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
                // Gentle, low-volume two-tone bell with a soft attack and long decay.
                const int rate = 22050, length = 5954;
                double fundamental = frequency == 250 ? 329.63 : frequency == 900 ? 440.0 : 392.0;
                byte[] pcm = new byte[length * 2];
                for (int i = 0; i < length; i++)
                {
                    double t = i / (double)rate;
                    double fadeIn = Math.Min(1, t / 0.035);
                    double fadeOut = Math.Min(1, (length - 1 - i) / (rate * 0.09));
                    double envelope = Math.Max(0, fadeIn * fadeOut) * Math.Exp(-t * 5);
                    double tone = Math.Sin(2 * Math.PI * fundamental * t) + 0.20 * Math.Sin(2 * Math.PI * fundamental * 1.5 * t);
                    short sample = (short)(1150 * envelope * tone);
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
