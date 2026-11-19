using System.Net;
using System.Text;
using IvySay;
using Xunit;

public class GroqClientTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> callback) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => callback(request, token);
    }
    [Fact]
    public async Task WarmupUsesAuthenticatedModelsEndpointWithoutBlockingOnErrors()
    {
        bool called = false;
        using var client = new GroqClient(new StubHandler((request, _) =>
        {
            called = true;
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.Equal("https://api.groq.com/openai/v1/models", request.RequestUri!.ToString());
            Assert.Equal("test-key", request.Headers.Authorization!.Parameter);
            Assert.Null(request.Content);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        }));
        await client.WarmConnectionAsync("test-key", CancellationToken.None);
        Assert.True(called);
    }
    [Fact]
    public async Task WarmupIgnoresNetworkFailure()
    {
        using var client = new GroqClient(new StubHandler((_, _) => throw new HttpRequestException("offline")));
        await client.WarmConnectionAsync("test-key", CancellationToken.None);
    }
    [Fact]
    public async Task TranscriptionUsesMemoryMultipartAndConfiguredModel()
    {
        using var client = new GroqClient(new StubHandler(async (request, token) =>
        {
            Assert.Equal("https://api.groq.com/openai/v1/audio/transcriptions", request.RequestUri!.ToString());
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal("test-key", request.Headers.Authorization.Parameter);
            Assert.Equal("multipart/form-data", request.Content!.Headers.ContentType!.MediaType);
            var body = await request.Content.ReadAsStringAsync(token);
            Assert.Contains("whisper-large-v3-turbo", body);
            Assert.Contains("audio.wav", body);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"text\":\" hello \"}", Encoding.UTF8, "application/json") };
        }));
        Assert.Equal("hello", await client.TranscribeAsync(new byte[] { 1, 2 }, "test-key", "whisper-large-v3-turbo", CancellationToken.None));
    }
    [Fact]
    public async Task FormatterSendsTranscriptAsUserData()
    {
        using var client = new GroqClient(new StubHandler(async (request, token) =>
        {
            Assert.EndsWith("/chat/completions", request.RequestUri!.AbsolutePath);
            string body = await request.Content!.ReadAsStringAsync(token);
            Assert.Contains("openai/gpt-oss-20b", body);
            Assert.Contains("\"reasoning_effort\":\"low\"", body);
            Assert.Contains("\"reasoning_format\":\"hidden\"", body);
            Assert.Contains("\"max_completion_tokens\":2048", body);
            Assert.Contains("Bullet buy milk", body);
            Assert.Contains("self-corrections", body);
            Assert.Contains("accent-related", body);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"content\":\"- Buy milk\"}}]}", Encoding.UTF8, "application/json") };
        }));
        Assert.Equal("- Buy milk", await client.PolishAsync("Bullet buy milk", "test-key", CancellationToken.None));
    }
    [Fact]
    public async Task ErrorMessageDoesNotExposeProviderBody()
    {
        using var client = new GroqClient(new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("secret provider body") })));
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => client.TranscribeAsync(new byte[] { 1, 2 }, "test-key", "whisper-large-v3", CancellationToken.None));
        Assert.Equal(HttpStatusCode.Unauthorized, error.StatusCode);
        Assert.DoesNotContain("secret provider body", error.Message);
        Assert.DoesNotContain("test-key", error.Message);
    }
    [Fact]
    public async Task TruncatedFormatterOutputIsRejected()
    {
        using var client = new GroqClient(new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"choices\":[{\"finish_reason\":\"length\",\"message\":{\"content\":\"incomplete\"}}]}") })));
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.PolishAsync("some speech", "test-key", CancellationToken.None));
    }
}
