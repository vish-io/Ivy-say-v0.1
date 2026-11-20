using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using IvySay;
using Xunit;

public class FormatterRouterTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => callback(request, token);
    }
    [Fact]
    public async Task PrimaryRateLimitUsesFallbackExactlyOnce()
    {
        var models = new List<string>();
        bool fallbackNotified = false;
        var fragments = new List<string>();
        string result = await FormatterRouter.RunAsync(async (model, callback, _) =>
        {
            models.Add(model);
            if (model == FormatterRouter.Primary) throw new HttpRequestException("limited", null, HttpStatusCode.TooManyRequests);
            await callback("polished");
            return "polished";
        }, fragment => { fragments.Add(fragment); return Task.CompletedTask; }, () => fallbackNotified = true, false, CancellationToken.None);
        Assert.Equal(new[] { FormatterRouter.Primary, FormatterRouter.Fallback }, models);
        Assert.True(fallbackNotified);
        Assert.Equal("polished", result);
        Assert.Equal(new[] { "polished" }, fragments);
    }
    [Fact]
    public async Task PreviewCanDiscardPartialPrimaryBeforeRetry()
    {
        var preview = new StringBuilder();
        int calls = 0;
        string result = await FormatterRouter.RunAsync(async (model, callback, _) =>
        {
            calls++;
            if (model == FormatterRouter.Primary) { await callback("unfinished"); throw new IOException("connection lost"); }
            await callback("complete"); return "complete";
        }, fragment => { preview.Append(fragment); return Task.CompletedTask; }, () => preview.Clear(), false, CancellationToken.None);
        Assert.Equal(2, calls);
        Assert.Equal("complete", preview.ToString());
        Assert.Equal("complete", result);
    }
    [Fact]
    public async Task DirectTypingNeverRetriesAfterFirstPossibleInsertion()
    {
        int calls = 0;
        await Assert.ThrowsAsync<IOException>(() => FormatterRouter.RunAsync(async (_, callback, _) =>
        {
            calls++; await callback("partial"); throw new IOException("connection lost");
        }, _ => Task.CompletedTask, () => throw new Exception("Must not retry"), true, CancellationToken.None));
        Assert.Equal(1, calls);
    }
    [Fact]
    public async Task DirectTypingCanFallbackBeforeAnyFragment()
    {
        var models = new List<string>();
        string result = await FormatterRouter.RunAsync(async (model, callback, _) =>
        {
            models.Add(model);
            if (model == FormatterRouter.Primary) throw new HttpRequestException("unavailable", null, HttpStatusCode.ServiceUnavailable);
            await callback("safe"); return "safe";
        }, _ => Task.CompletedTask, () => { }, true, CancellationToken.None);
        Assert.Equal("safe", result);
        Assert.Equal(new[] { FormatterRouter.Primary, FormatterRouter.Fallback }, models);
    }
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task DoesNotRetryAuthenticationOrBadRequests(HttpStatusCode code)
    {
        int calls = 0;
        await Assert.ThrowsAsync<HttpRequestException>(() => FormatterRouter.RunAsync((_, _, _) =>
        {
            calls++; throw new HttpRequestException("no", null, code);
        }, _ => Task.CompletedTask, () => { }, false, CancellationToken.None));
        Assert.Equal(1, calls);
    }
    [Fact]
    public async Task DoesNotRetryAfterCallerCancellation()
    {
        using var cts = new CancellationTokenSource();
        int calls = 0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => FormatterRouter.RunAsync((_, _, _) =>
        {
            calls++; cts.Cancel(); throw new OperationCanceledException();
        }, _ => Task.CompletedTask, () => { }, false, cts.Token));
        Assert.Equal(1, calls);
    }
    [Fact]
    public async Task DoesNotRetryFallbackItself()
    {
        int calls = 0;
        await Assert.ThrowsAsync<HttpRequestException>(() => FormatterRouter.RunAsync((_, _, _) =>
        {
            calls++; throw new HttpRequestException("limited", null, HttpStatusCode.TooManyRequests);
        }, _ => Task.CompletedTask, () => { }, false, CancellationToken.None));
        Assert.Equal(2, calls);
    }
    [Fact]
    public async Task LiveRequestShapeUsesFallbackModelWithSameFormattingParameters()
    {
        var models = new List<string>();
        using var client = new GroqClient(new StubHandler(async (request, token) =>
        {
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            var root = body.RootElement;
            string model = root.GetProperty("model").GetString()!;
            models.Add(model);
            Assert.Equal("low", root.GetProperty("reasoning_effort").GetString());
            Assert.Equal("hidden", root.GetProperty("reasoning_format").GetString());
            Assert.True(root.GetProperty("stream").GetBoolean());
            Assert.Equal("test-key", request.Headers.Authorization!.Parameter);
            if (model == FormatterRouter.Primary) return new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            string sse = "data: {\"choices\":[{\"delta\":{\"content\":\"Ready\"},\"finish_reason\":\"stop\"}]}\n\ndata: [DONE]\n\n";
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(sse, Encoding.UTF8, "text/event-stream") };
        }));
        string result = await FormatterRouter.RunAsync((model, callback, token) => client.PolishStreamingAsync("speech", "test-key", model, callback, token), _ => Task.CompletedTask, () => { }, false, CancellationToken.None);
        Assert.Equal("Ready", result);
        Assert.Equal(new[] { FormatterRouter.Primary, FormatterRouter.Fallback }, models);
    }
}
