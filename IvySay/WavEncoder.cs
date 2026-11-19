using System.Text;
namespace IvySay;
internal static class WavEncoder
{
    public static byte[] Encode16KhzMono(byte[] pcm)
    {
        if ((pcm.Length & 1) != 0) throw new ArgumentException("PCM must contain complete 16-bit samples.", nameof(pcm));
        using var output = new MemoryStream();
        using (var writer = new BinaryWriter(output, Encoding.ASCII, leaveOpen: true))
        {
            writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(checked(36 + pcm.Length));
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
            writer.Write((short)1); writer.Write((short)1); writer.Write(16000); writer.Write(32000);
            writer.Write((short)2); writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(pcm.Length);
            writer.Write(pcm);
        }
        return output.ToArray();
    }
}
