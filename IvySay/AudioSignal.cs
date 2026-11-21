namespace IvySay;

// Very conservative electrical-signal check, not voice activity detection.
// It cannot distinguish speech from TV, noise, or the wrong microphone.
internal readonly record struct AudioSignal(double Rms, double ClippedFraction, double DurationSeconds)
{
    public bool IsAlmostSilent => Rms < 2.0;
    public string Description
    {
        get
        {
            string level = Rms > 0 ? $"{20 * Math.Log10(Rms / 32768):F0} dBFS RMS" : "no measurable signal";
            string warning = Rms < 64 && !IsAlmostSilent ? " · very low level; check microphone and input volume" : ClippedFraction > 0.01 ? " · clipping detected; lower input volume" : "";
            return $"{DurationSeconds:F1}s · {level}{warning}";
        }
    }
    public static AudioSignal Measure(ReadOnlySpan<byte> pcm)
    {
        if (pcm.Length % 2 != 0) throw new ArgumentException("Incomplete PCM sample.");
        int count = pcm.Length / 2;
        if (count == 0) return new AudioSignal(0, 0, 0);
        long sum = 0, squares = 0; int clipped = 0;
        for (int i = 0; i < pcm.Length; i += 2)
        {
            int sample = (short)(pcm[i] | pcm[i + 1] << 8);
            sum += sample; squares += (long)sample * sample;
            if (sample >= 32700 || sample <= -32700) clipped++;
        }
        double mean = (double)sum / count;
        // Remove DC offset so a stuck microphone does not look like usable audio.
        double rms = Math.Sqrt(Math.Max(0, (double)squares / count - mean * mean));
        return new AudioSignal(rms, (double)clipped / count, count / 16000d);
    }
    public static void RequireSignal(byte[] pcm)
    {
        if (Measure(pcm).IsAlmostSilent)
            throw new InvalidOperationException("Almost no microphone signal was captured. Check the selected input, mute, Windows microphone permissions and input volume, then try again.");
    }
}
