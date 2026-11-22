using System.Net;
using System.Net.Http;
using System.Text;
using IvySay;
using Xunit;

public class GroqConnectionPoolTests
{
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public bool Disposed { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => send(request, token);
        protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
    }
    [Fact] public async Task OfflineFailureReplacesPoolForNextRecordingWithoutRetryingOriginal()
    {
        int created = 0, calls = 0;
        using var client = new GroqClient(() => {
            int generation = ++created;
            return new Handler((_, _) => { calls++; if (generation == 1) throw new HttpRequestException("offline"); return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"text\":\"back online\"}", Encoding.UTF8, "application/json") }); });
        });
        await Assert.ThrowsAsync<HttpRequestException>(() => client.TranscribeAsync(new byte[] { 1 }, "key", "whisper-large-v3", "auto", "", CancellationToken.None));
        Assert.Equal(1, calls);
        Assert.Equal("back online", await client.TranscribeAsync(new byte[] { 1 }, "key", "whisper-large-v3", "auto", "", CancellationToken.None));
        Assert.Equal(2, created); Assert.Equal(2, calls);
    }
    [Fact] public async Task HttpErrorsDoNotRotateOrAutomaticallyRetry()
    {
        int created = 0, calls = 0;
        using var client = new GroqClient(() => { created++; return new Handler((_, _) => { calls++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)); }); });
        for (int i = 0; i < 2; i++) await Assert.ThrowsAsync<HttpRequestException>(() => client.TranscribeAsync(new byte[] { 1 }, "key", "whisper-large-v3", "auto", "", CancellationToken.None));
        Assert.Equal(1, created); Assert.Equal(2, calls);
    }
    [Fact] public void NetworkChangeRetiresOldPoolOnlyAfterAllRequestsFinish()
    {
        var handlers = new List<Handler>();
        using var pool = new GroqConnectionPool(() => { var h = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))); handlers.Add(h); return h; });
        var first = pool.Acquire(); var concurrent = pool.Acquire();
        pool.NotifyNetworkChanged();
        using var next = pool.Acquire();
        Assert.Equal(2, handlers.Count); Assert.False(handlers[0].Disposed);
        pool.Failed(first); // Late failure must not invalidate the new generation.
        using var again = pool.Acquire(); Assert.Equal(2, handlers.Count);
        first.Dispose(); Assert.False(handlers[0].Disposed);
        concurrent.Dispose(); Assert.True(handlers[0].Disposed); Assert.False(handlers[1].Disposed);
    }
    [Fact] public void ShutdownAllowsInflightLeaseToFinishAndRejectsNewLeases()
    {
        Handler? handler = null;
        var pool = new GroqConnectionPool(() => handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));
        var lease = pool.Acquire(); pool.Dispose(); Assert.False(handler!.Disposed);
        Assert.Throws<ObjectDisposedException>(() => pool.Acquire()); lease.Dispose(); Assert.True(handler.Disposed);
    }
}
