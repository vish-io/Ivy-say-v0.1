using IvySay;
using Xunit;

public class AudioSignalTests
{
    [Fact] public void SilenceAndStuckDcAreRejected()
    {
        Assert.True(AudioSignal.Measure(new byte[32000]).IsAlmostSilent);
        byte[] dc = Enumerable.Repeat((byte)0x12, 32000).ToArray();
        Assert.True(AudioSignal.Measure(dc).IsAlmostSilent);
        Assert.Throws<InvalidOperationException>(() => AudioSignal.RequireSignal(dc));
    }
    [Fact] public void QuietVariableSignalIsNotAssumedToBeSilence()
    {
        byte[] pcm = new byte[32000];
        for (int i = 0; i < pcm.Length; i += 2) { short value = (short)((i / 2 % 2 == 0) ? 10 : -10); pcm[i] = (byte)value; pcm[i + 1] = (byte)(value >> 8); }
        AudioSignal info = AudioSignal.Measure(pcm);
        Assert.False(info.IsAlmostSilent);
        Assert.Equal(1, info.DurationSeconds);
        Assert.Contains("very low", info.Description);
    }
    [Fact] public void ClippingAndIncompleteSamplesAreDetected()
    {
        byte[] pcm = { 0xff, 0x7f, 0, 0x80, 0xff, 0x7f, 0, 0x80 };
        Assert.Contains("clipping", AudioSignal.Measure(pcm).Description);
        Assert.Throws<ArgumentException>(() => AudioSignal.Measure(new byte[1]));
    }
    [Fact] public async Task SilenceIsNotSentToTranscriber()
    {
        int calls = 0;
        var session = new ChunkedTranscription(14, 0, "", (_, _, _, _) => { calls++; return Task.FromResult("invented text"); }, CancellationToken.None);
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.FinishAsync(WavEncoder.Encode16KhzMono(new byte[32000])));
        Assert.Equal(0, calls);
    }
}
