using System.Net;
using System.Text;
using System.Text.Json;
using IvySay;
using Xunit;

public class StreamingCompletionTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => callback(request, token);
    }
    private static MemoryStream Sse(string content) => new(Encoding.UTF8.GetBytes(content));
    private const string Chunk = "data: {\"choices\":[{\"delta\":{\"content\":\"Hello\"},\"finish_reason\":null}]}\r\n\r\n";
    private const string Last = "data: {\"choices\":[{\"delta\":{\"content\":\" world\"},\"finish_reason\":\"stop\"}]}\n\n";
    private const string Done = "data: [DONE]\n\n";
    [Fact]
    public async Task DeliversFragmentsAndRequiresCompletedStream()
    {
        using var stream = Sse(": keepalive\n\n" + Chunk + Last + "data: {\"choices\":[]}\n\n" + Done);
        var fragments = new List<string>();
        string result = await StreamingCompletion.ReadAsync(stream, text => { fragments.Add(text); return Task.CompletedTask; }, CancellationToken.None);
        Assert.Equal("Hello world", result);
        Assert.Equal(new[] { "Hello", " world" }, fragments);
    }
    private sealed class FragmentedStream(byte[] bytes) : Stream
    {
        private readonly MemoryStream inner = new(bytes);
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => inner.Read(buffer, offset, Math.Min(count, 1));
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default) => inner.ReadAsync(buffer[..Math.Min(buffer.Length, 1)], token);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
    }
    private sealed class GatedStream : Stream
    {
        private readonly byte[] first = Encoding.UTF8.GetBytes(Chunk);
        private readonly byte[] last = Encoding.UTF8.GetBytes(Last + Done);
        private readonly TaskCompletionSource<bool> gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int phase;
        public void Continue() => gate.TrySetResult(true);
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
        {
            if (phase == 0) { phase++; first.CopyTo(buffer); return first.Length; }
            if (phase == 1) { await gate.Task.WaitAsync(token); phase++; last.CopyTo(buffer); return last.Length; }
            return 0;
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
    [Fact]
    public async Task EmitsPreviewBeforeTheNetworkStreamFinishes()
    {
        using var stream = new GatedStream();
        var firstSeen = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var task = StreamingCompletion.ReadAsync(stream, text => { firstSeen.TrySetResult(text); return Task.CompletedTask; }, CancellationToken.None);
        try { Assert.Equal("Hello", await firstSeen.Task.WaitAsync(TimeSpan.FromSeconds(3))); }
        finally { stream.Continue(); }
        Assert.Equal("Hello world", await task);
    }
    [Fact]
    public async Task HandlesFragmentedNetworkReadsAndUnicode()
    {
        const string message = "data: {\"choices\":[{\"delta\":{\"content\":\"ਨਮਸਤੇ 😀\"},\"finish_reason\":\"stop\"}]}\r\n\r\ndata: [DONE]\r\n\r\n";
        using var stream = new FragmentedStream(Encoding.UTF8.GetBytes(message));
        Assert.Equal("ਨਮਸਤੇ 😀", await StreamingCompletion.ReadAsync(stream, _ => Task.CompletedTask, CancellationToken.None));
    }
    [Fact]
    public async Task PropagatesInsertionFailureWithoutDeliveringMoreFragments()
    {
        using var stream = Sse(Chunk + Last + Done);
        int calls = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(() => StreamingCompletion.ReadAsync(stream, _ => { calls++; throw new InvalidOperationException("focus lost"); }, CancellationToken.None));
        Assert.Equal(1, calls);
    }
    [Fact]
    public async Task RejectsTruncatedAndUnfinishedStreams()
    {
        using var truncated = Sse(Chunk);
        var received = new List<string>();
        await Assert.ThrowsAsync<IOException>(() => StreamingCompletion.ReadAsync(truncated, x => { received.Add(x); return Task.CompletedTask; }, CancellationToken.None));
        Assert.Equal(new[] { "Hello" }, received);
        using var noStop = Sse(Chunk + Done);
        await Assert.ThrowsAsync<InvalidOperationException>(() => StreamingCompletion.ReadAsync(noStop, _ => Task.CompletedTask, CancellationToken.None));
    }
    [Fact]
    public async Task RejectsIncompleteOrOversizedEvents()
    {
        using var stream = Sse("data: {\"choices\":[{\"delta\":{},\"finish_reason\":\"length\"}]}\n\n" + Done);
        await Assert.ThrowsAsync<InvalidOperationException>(() => StreamingCompletion.ReadAsync(stream, _ => Task.CompletedTask, CancellationToken.None));
        using var oversized = Sse("data: " + new string('x', 70_000) + "\n\n");
        await Assert.ThrowsAsync<InvalidOperationException>(() => StreamingCompletion.ReadAsync(oversized, _ => Task.CompletedTask, CancellationToken.None));
    }
    [Fact]
    public async Task StreamingRequestUsesExistingModelAndReportsActualDeltas()
    {
        using var client = new GroqClient(new StubHandler(async (request, token) =>
        {
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            Assert.True(json.RootElement.GetProperty("stream").GetBoolean());
            Assert.Equal("openai/gpt-oss-20b", json.RootElement.GetProperty("model").GetString());
            Assert.Equal("hidden", json.RootElement.GetProperty("reasoning_format").GetString());
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Chunk + Last + Done, Encoding.UTF8, "text/event-stream") };
        }));
        var seen = new List<string>();
        string result = await client.PolishStreamingAsync("hello", "test-key", FormatterRouter.Primary, fragment => { seen.Add(fragment); return Task.CompletedTask; }, CancellationToken.None);
        Assert.Equal("Hello world", result);
        Assert.Equal(new[] { "Hello", " world" }, seen);
    }
}
