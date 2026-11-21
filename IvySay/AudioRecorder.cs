using System.IO;
using NAudio.Wave;

namespace IvySay;
internal sealed class AudioRecorder : IDisposable
{
    private readonly WaveInEvent input;
    private readonly MemoryStream pcm = new();
    private readonly TaskCompletionSource<bool> firstBuffer = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<Exception?> stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly object sync = new();
    private bool limitReached;
    // Bound memory usage and upload size to two minutes of 16 kHz mono PCM.
    private const int MaxBytes = 16000 * 2 * 120;
    public AudioRecorder(string? microphone)
    {
        int deviceNumber = -1; // WAVE_MAPPER follows the Windows default recording device.
        if (!string.IsNullOrEmpty(microphone))
        {
            int[] matches = Enumerable.Range(0, WaveIn.DeviceCount)
                .Where(i => WaveIn.GetCapabilities(i).ProductName == microphone).ToArray();
            if (matches.Length == 0) throw new InvalidOperationException("The selected microphone is unavailable. Choose another in settings.");
            // WinMM product names can be truncated or shared; selecting the first
            // match can silently switch inputs after a reboot or device reorder.
            if (matches.Length > 1) throw new InvalidOperationException("Multiple microphones have the same Windows recording name. Ivy Say cannot safely identify the selected device. Set the intended microphone as the Windows default, then select Windows default in Ivy Say.");
            deviceNumber = matches[0];
        }
        input = new WaveInEvent { DeviceNumber = deviceNumber, WaveFormat = new WaveFormat(16000, 16, 1), BufferMilliseconds = 50 };
        input.DataAvailable += (_, e) => { lock (sync) { int remaining = MaxBytes - (int)pcm.Length; if (remaining > 0) pcm.Write(e.Buffer, 0, Math.Min(remaining, e.BytesRecorded)); if (e.BytesRecorded > remaining) limitReached = true; } if (e.BytesRecorded > 0) firstBuffer.TrySetResult(true); };
        input.RecordingStopped += (_, e) => stopped.TrySetResult(e.Exception);
    }
    public async Task StartAsync(CancellationToken token)
    {
        input.StartRecording();
        Task completed = await Task.WhenAny(firstBuffer.Task, stopped.Task).WaitAsync(TimeSpan.FromSeconds(5), token);
        if (completed == stopped.Task)
        {
            Exception? error = await stopped.Task;
            throw new InvalidOperationException("Microphone stopped before recording began. Check the selected input and Windows permissions.", error);
        }
    }
    public byte[] SnapshotPcm() { lock (sync) return pcm.ToArray(); }
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
