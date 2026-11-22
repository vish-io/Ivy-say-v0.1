using NAudio.Wave;
using System.IO;
namespace IvySay;
internal static class StatusSounds
{
    public static void Play(bool enabled, int frequency)
    {
        if (!enabled) return;
        _ = Task.Run(() =>
        {
            try
            {
                const int rate = 22050, length = 1764; // 80ms with soft envelope
                byte[] pcm = new byte[length * 2];
                for (int i = 0; i < length; i++)
                {
                    double envelope = Math.Sin(Math.PI * i / length);
                    short sample = (short)(2500 * envelope * Math.Sin(2 * Math.PI * frequency * i / rate));
                    pcm[i * 2] = (byte)sample; pcm[i * 2 + 1] = (byte)(sample >> 8);
                }
                using var output = new WaveOutEvent { DesiredLatency = 80 };
                using var stream = new RawSourceWaveStream(new MemoryStream(pcm), new WaveFormat(rate, 16, 1));
                output.Init(stream); output.Play();
                while (output.PlaybackState == PlaybackState.Playing) Thread.Sleep(20);
            }
            catch { /* Audio feedback is optional and must never interrupt dictation. */ }
        });
    }
}
