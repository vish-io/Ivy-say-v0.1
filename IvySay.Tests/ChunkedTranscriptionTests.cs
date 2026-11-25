using IvySay;
using Xunit;

public class ChunkedTranscriptionTests
{
    private static byte[] Speech(int seconds)
    {
        var pcm = new byte[seconds * 32000];
        for (int i = 0; i < pcm.Length; i += 2) { short sample = (short)(10000 * Math.Sin(i * Math.PI / 32)); pcm[i] = (byte)sample; pcm[i + 1] = (byte)(sample >> 8); }
        return pcm;
    }
    private static void Pause(byte[] pcm, double at) => Array.Clear(pcm, (int)(at * 32000), 32000 / 2);
    [Fact] public void SilenceDetectionRequiresSustainedQuiet()
    {
        var pcm = Speech(12); Pause(pcm, 8);
        int cut = ChunkedTranscription.FindPause(pcm, 7 * 32000);
        Assert.InRange(cut, 8 * 32000, (int)(8.5 * 32000));
        Assert.Equal(-1, ChunkedTranscription.FindPause(Speech(12), 7 * 32000));
    }
    [Fact] public void StitchingIsConservative()
    {
        Assert.True(ChunkedTranscription.TryJoin("please send the file", "the file tomorrow", out string joined));
        Assert.Equal("please send the file tomorrow", joined);
        Assert.False(ChunkedTranscription.TryJoin("very", "very important", out _));
        Assert.False(ChunkedTranscription.TryJoin("very very", "very very important", out _));
        Assert.True(ChunkedTranscription.TryJoin("one", "two", out joined));
        Assert.Equal("one two", joined);
    }
    [Theory] [InlineData(24)] [InlineData(32)] [InlineData(48)]
    public void AudioEncodingProducesOgg(int bitrate)
    {
        var encoded = AudioPayload.Encode(Speech(1), bitrate);
        Assert.Equal("OggS", System.Text.Encoding.ASCII.GetString(encoded, 0, 4));
    }
    [Fact] public async Task LongAudioUsesChunksAndWholeTranscript()
    {
        var pcm = Speech(16); Pause(pcm, 8);
        int calls = 0;
        var session = new ChunkedTranscription(14, 0, "", (audio, opus, prompt, token) => Task.FromResult(Interlocked.Increment(ref calls) == 1 ? "please send the file" : "the file tomorrow"), CancellationToken.None);
        session.Update(pcm);
        Assert.Equal(1, session.ChunkCount);
        string result = await session.FinishAsync(WavEncoder.Encode16KhzMono(pcm));
        Assert.Equal("please send the file tomorrow", result);
        Assert.False(session.UsedFullRecordingFallback);
        Assert.Equal(2, calls);
    }
    [Fact] public async Task LaterCorrectionIsJoinedWithEarlierStatement()
    {
        var pcm = Speech(16); Pause(pcm, 8);
        int calls = 0;
        var session = new ChunkedTranscription(14, 0, "", (_, _, _, _) =>
            Task.FromResult(Interlocked.Increment(ref calls) == 1
                ? "Send the report on Monday." : "Actually, send it on Tuesday."), CancellationToken.None);
        session.Update(pcm);
        string result = await session.FinishAsync(WavEncoder.Encode16KhzMono(pcm));
        Assert.Equal("Send the report on Monday. Actually, send it on Tuesday.", result);
        Assert.False(session.UsedFullRecordingFallback);
    }
    [Fact] public async Task FailureRetriesOriginalAsWav()
    {
        var pcm = Speech(16); Pause(pcm, 8);
        int calls = 0;
        var session = new ChunkedTranscription(14, 0, "", (audio, opus, prompt, token) => {
            int call = Interlocked.Increment(ref calls);
            if (call == 1) throw new HttpRequestException("failure");
            Assert.False(opus);
            Assert.Equal(pcm.Length + 44, audio.Length);
            return Task.FromResult("full recording");
        }, CancellationToken.None);
        session.Update(pcm);
        Assert.Equal("full recording", await session.FinishAsync(WavEncoder.Encode16KhzMono(pcm)));
        Assert.True(session.UsedFullRecordingFallback);
    }
    [Fact] public async Task ShortOrUninterruptedAudioStaysWhole()
    {
        var pcm = Speech(16); int calls = 0;
        var session = new ChunkedTranscription(14, 0, "", (audio, opus, prompt, token) => { calls++; return Task.FromResult("complete"); }, CancellationToken.None);
        session.Update(pcm);
        Assert.Equal(0, session.ChunkCount);
        Assert.Equal("complete", await session.FinishAsync(WavEncoder.Encode16KhzMono(pcm)));
        Assert.Equal(1, calls);
    }
    [Fact] public void DoesNotSplitJustBeforeTheEnd()
    {
        var pcm = Speech(15); Pause(pcm, 14.2);
        var session = new ChunkedTranscription(14, 0, "", (_, _, _, _) => Task.FromResult("text"), CancellationToken.None);
        session.Update(pcm);
        Assert.Equal(0, session.ChunkCount);
    }
    [Fact] public async Task PreviousTranscriptIsNotUsedAsPrompt()
    {
        var pcm = Speech(16); Pause(pcm, 8);
        var prompts = new List<string>();
        var session = new ChunkedTranscription(14, 0, "example term", (_, _, prompt, _) => {
            prompts.Add(prompt); return Task.FromResult(prompts.Count == 1 ? "an unrelated sentence" : "different words");
        }, CancellationToken.None);
        session.Update(pcm);
        await session.FinishAsync(WavEncoder.Encode16KhzMono(pcm));
        Assert.Equal(new[] { "example term", "example term" }, prompts);
    }

}
