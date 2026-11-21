using System.IO;
using Concentus;
using Concentus.Enums;
using Concentus.Oggfile;

namespace IvySay;
internal static class AudioPayload
{
    // Encoding happens in memory on a worker thread. The original PCM remains available.
    public static byte[] Encode(byte[] pcm, int kbps)
    {
        if (kbps == 0) return WavEncoder.Encode16KhzMono(pcm);
        if (kbps is not (24 or 32 or 48)) throw new ArgumentOutOfRangeException(nameof(kbps));
        if ((pcm.Length & 1) != 0) throw new ArgumentException("Incomplete PCM sample.");
        short[] samples = new short[pcm.Length / 2];
        for (int i = 0; i < samples.Length; i++) samples[i] = (short)(pcm[i * 2] | (pcm[i * 2 + 1] << 8));
        using var output = new MemoryStream();
        var encoder = OpusCodecFactory.CreateEncoder(16000, 1, OpusApplication.OPUS_APPLICATION_VOIP);
        encoder.Bitrate = kbps * 1000;
        var ogg = new OpusOggWriteStream(encoder, output, inputSampleRate: 16000, leaveOpen: true);
        ogg.WriteSamples(samples, 0, samples.Length);
        ogg.Finish();
        return output.ToArray();
    }
}
