using System.Text;
using IvySay;
using Xunit;

public class WavEncoderTests
{
    [Fact]
    public void EncodesPcmWithCorrectRiffHeader()
    {
        byte[] pcm = { 1, 2, 3, 4 };
        byte[] wav = WavEncoder.Encode16KhzMono(pcm);
        Assert.Equal(48, wav.Length);
        Assert.Equal("RIFF", Encoding.ASCII.GetString(wav, 0, 4));
        Assert.Equal(40, BitConverter.ToInt32(wav, 4));
        Assert.Equal("WAVEfmt ", Encoding.ASCII.GetString(wav, 8, 8));
        Assert.Equal(16000, BitConverter.ToInt32(wav, 24));
        Assert.Equal(32000, BitConverter.ToInt32(wav, 28));
        Assert.Equal("data", Encoding.ASCII.GetString(wav, 36, 4));
        Assert.Equal(4, BitConverter.ToInt32(wav, 40));
        Assert.Equal(pcm, wav[44..]);
    }
    [Fact] public void RejectsIncompleteSample() => Assert.Throws<ArgumentException>(() => WavEncoder.Encode16KhzMono(new byte[1]));
}
