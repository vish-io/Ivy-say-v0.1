using System.IO;
using NAudio.Wave;

namespace IvySay;
internal sealed class AudioRecorder : IDisposable
{
    private readonly WaveInEvent input;
    private readonly MemoryStream pcm = new();
    private readonly TaskCompletionSource<Exception?> stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly object sync = new();
    private bool limitReached;
    // Bound memory usage and upload size to two minutes of 16 kHz mono PCM.
    private const int MaxBytes = 16000 * 2 * 120;
    public AudioRecorder()
    {
        input = new WaveInEvent { WaveFormat = new WaveFormat(16000, 16, 1), BufferMilliseconds = 50 };
        input.DataAvailable += (_, e) => { lock (sync) { int remaining = MaxBytes - (int)pcm.Length; if (remaining > 0) pcm.Write(e.Buffer, 0, Math.Min(remaining, e.BytesRecorded)); if (e.BytesRecorded > remaining) limitReached = true; } };
        input.RecordingStopped += (_, e) => stopped.TrySetResult(e.Exception);
    }
    public void Start() => input.StartRecording();
    public async Task<byte[]> StopAsync()
    {
        input.StopRecording();
        Exception? error = await stopped.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (error != null) throw new InvalidOperationException("Microphone recording failed.", error);
        lock (sync)
        {
            if (limitReached) throw new InvalidOperationException("Recording exceeded the two-minute limit. Please try a shorter dictation.");
            if (pcm.Length < 3200) throw new InvalidOperationException("Recording was too short. Please try again.");
            return WavEncoder.Encode16KhzMono(pcm.ToArray());
        }
    }
    public void Dispose() { input.Dispose(); pcm.Dispose(); }
}
