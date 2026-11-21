using System.Text.RegularExpressions;

namespace IvySay;
// Holds the original audio in the recorder, and only ever emits text after all chunks complete.
internal sealed class ChunkedTranscription
{
    const int BytesPerSecond = 32000, Overlap = 8000;
    private readonly Func<byte[], bool, string, CancellationToken, Task<string>> send;
    private readonly int threshold, kbps;
    private readonly string hints;
    private readonly CancellationToken token;
    private readonly List<Task<string>> parts = new();
    private int end;
    private bool finished;
    public bool UsedFullRecordingFallback { get; private set; }
    public int ChunkCount => parts.Count;
    public ChunkedTranscription(int thresholdSeconds, int compressionKbps, string hints,
        Func<byte[], bool, string, CancellationToken, Task<string>> send, CancellationToken token)
    {
        threshold = Math.Clamp(thresholdSeconds, 14, 120) * BytesPerSecond;
        kbps = compressionKbps; this.hints = hints; this.send = send; this.token = token;
    }
    public void Update(byte[] pcm)
    {
        if (finished || pcm.Length < threshold) return;
        // Only start at a detected 350 ms quiet interval, never force a cut in speech.
        while (true)
        {
            int cut = FindPause(pcm, end + 7 * BytesPerSecond);
            if (cut <= end || cut >= pcm.Length) break;
            int start = end == 0 ? 0 : Math.Max(0, end - Overlap);
            Queue(pcm[start..cut]);
            end = cut;
        }
    }
    private void Queue(byte[] pcm)
    {
        Task<string>? previous = parts.Count == 0 ? null : parts[^1];
        parts.Add(Task.Run(async () =>
        {
            string prompt = hints;
            if (previous != null)
            {
                string last = await previous.ConfigureAwait(false);
                // Preserve priority for the user's hints; only append context that fits.
                var words = last.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                string context = string.Join(" ", words.TakeLast(20));
                int available = 160 - prompt.Length - (prompt.Length > 0 ? 2 : 0);
                if (available > 0 && context.Length <= available && context.Length > 0)
                    prompt += (prompt.Length > 0 ? ", " : "") + context;
            }
            token.ThrowIfCancellationRequested();
            byte[] payload;
            bool compressed = kbps != 0;
            try { payload = AudioPayload.Encode(pcm, kbps); }
            catch when (!token.IsCancellationRequested) { compressed = false; payload = WavEncoder.Encode16KhzMono(pcm); }
            string result = await send(payload, compressed, prompt, token).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(result)) throw new InvalidOperationException("A transcription chunk was empty.");
            return result.Trim();
        }, token));
    }
    public async Task<string> FinishAsync(byte[] wav)
    {
        finished = true;
        if (wav.Length < 44) throw new ArgumentException("Invalid WAV.");
        byte[] pcm = wav[44..];
        if (parts.Count == 0) return await FullAsync(pcm, pcm.Length >= 8 * BytesPerSecond ? kbps : 0);
        if (end < pcm.Length) Queue(pcm[Math.Max(0, end - Overlap)..]);
        try
        {
            string[] results = await Task.WhenAll(parts).ConfigureAwait(false);
            string joined = results[0];
            foreach (string part in results.Skip(1))
            {
                if (!TryJoin(joined, part, out joined)) throw new InvalidOperationException("Ambiguous chunk boundary.");
            }
            return joined;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch
        {
            token.ThrowIfCancellationRequested();
            UsedFullRecordingFallback = true;
            // The fallback is deliberately lossless and is the same selected Whisper model.
            return await FullAsync(pcm, 0).ConfigureAwait(false);
        }
    }
    private async Task<string> FullAsync(byte[] pcm, int bitrate)
    {
        byte[] payload;
        bool compressed = bitrate != 0;
        try { payload = await Task.Run(() => AudioPayload.Encode(pcm, bitrate), token).ConfigureAwait(false); }
        catch when (!token.IsCancellationRequested) { compressed = false; payload = WavEncoder.Encode16KhzMono(pcm); }
        return await send(payload, compressed, hints, token).ConfigureAwait(false);
    }
    internal static int FindPause(byte[] pcm, int from)
    {
        // Fixed RMS gate: conservative, not a VAD and not a guarantee of a word boundary.
        const int frame = 320, frames = 35;
        int quiet = 0;
        for (int i = Math.Max(0, from / frame * frame); i + frame <= pcm.Length; i += frame)
        {
            long energy = 0;
            for (int j = i; j < i + frame; j += 2) { int s = (short)(pcm[j] | pcm[j + 1] << 8); energy += (long)s * s; }
            quiet = energy / (frame / 2) < 250L * 250 ? quiet + 1 : 0;
            if (quiet >= frames) return i + frame - frames * frame / 2;
        }
        return -1;
    }
    internal static bool TryJoin(string left, string right, out string joined)
    {
        var a = Regex.Matches(left, @"\S+").Cast<Match>().ToArray();
        var b = Regex.Matches(right, @"\S+").Cast<Match>().ToArray();
        static string Normal(string word) => word.Trim(' ', '.', ',', '!', '?', ':', ';', '"', '\'', '(', ')').ToLowerInvariant();
        int match = 0;
        for (int n = Math.Min(12, Math.Min(a.Length, b.Length)); n > 0; n--)
        {
            if (Enumerable.Range(0, n).All(i => Normal(a[a.Length - n + i].Value) == Normal(b[i].Value) && Normal(b[i].Value).Length > 0)) { match = n; break; }
        }
        // A single repeated word or a repeated identical phrase can be intentional; retry full audio.
        if (match == 1 || (match > 1 && Enumerable.Range(1, match - 1).All(i => Normal(b[i].Value) == Normal(b[0].Value)))) { joined = ""; return false; }
        string suffix = match == 0 ? right : right[(b[match - 1].Index + b[match - 1].Length)..].TrimStart();
        joined = suffix.Length == 0 ? left : left.TrimEnd() + " " + suffix;
        return true;
    }
}
