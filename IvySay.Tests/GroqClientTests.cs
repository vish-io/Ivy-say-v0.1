using System.Net;
using System.Text;
using System.Text.Json;
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
            Assert.Contains("name=language", body);
            Assert.Contains("\r\n\r\nen\r\n", body);
            Assert.Contains("name=prompt", body);
            Assert.Contains("Grindelwald", body);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"text\":\" hello \"}", Encoding.UTF8, "application/json") };
        }));
        Assert.Equal("hello", await client.TranscribeAsync(new byte[] { 1, 2 }, "test-key", "whisper-large-v3-turbo", "en", "Grindelwald", CancellationToken.None));
    }
    [Fact]
    public async Task AutoLanguageAndEmptyHintsAreNotSent()
    {
        using var client = new GroqClient(new StubHandler(async (request, token) =>
        {
            var body = await request.Content!.ReadAsStringAsync(token);
            Assert.DoesNotContain("name=language", body);
            Assert.DoesNotContain("name=prompt", body);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"text\":\"hello\"}") };
        }));
        Assert.Equal("hello", await client.TranscribeAsync(new byte[] { 1, 2 }, "test-key", "whisper-large-v3", "auto", "", CancellationToken.None));
    }
    [Fact]
    public async Task FormatterSendsTranscriptAsUserData()
    {
        using var client = new GroqClient(new StubHandler(async (request, token) =>
        {
            Assert.EndsWith("/chat/completions", request.RequestUri!.AbsolutePath);
            string body = await request.Content!.ReadAsStringAsync(token);
            using var payload = JsonDocument.Parse(body);
            var root = payload.RootElement;
            Assert.Equal("openai/gpt-oss-20b", root.GetProperty("model").GetString());
            Assert.Equal("low", root.GetProperty("reasoning_effort").GetString());
            Assert.Equal("hidden", root.GetProperty("reasoning_format").GetString());
            Assert.Equal(2048, root.GetProperty("max_completion_tokens").GetInt32());
            var messages = root.GetProperty("messages");
            Assert.Equal(2, messages.GetArrayLength());
            Assert.Equal("system", messages[0].GetProperty("role").GetString());
            string prompt = messages[0].GetProperty("content").GetString()!;
            Assert.True(prompt.Length < 2000, "The formatter prompt should remain compact.");
            Assert.Contains("self-corrections", prompt);
            Assert.Contains("accent-related", prompt);
            Assert.Contains("Only an opening command", prompt);
            Assert.Contains("do not answer questions", prompt);
            Assert.Equal("user", messages[1].GetProperty("role").GetString());
            Assert.Equal("Bullet buy milk", messages[1].GetProperty("content").GetString());
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"content\":\"- Buy milk\"}}]}", Encoding.UTF8, "application/json") };
        }));
        Assert.Equal("- Buy milk", await client.PolishAsync("Bullet buy milk", "test-key", CancellationToken.None));
    }
    [Fact]
    public async Task ErrorMessageDoesNotExposeProviderBody()
    {
        using var client = new GroqClient(new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("secret provider body") })));
        var error = await Assert.ThrowsAsync<HttpRequestException>(() => client.TranscribeAsync(new byte[] { 1, 2 }, "test-key", "whisper-large-v3", "auto", "", CancellationToken.None));
        Assert.Equal(HttpStatusCode.Unauthorized, error.StatusCode);
        Assert.DoesNotContain("secret provider body", error.Message);
        Assert.DoesNotContain("test-key", error.Message);
    }
    [Fact]
    public async Task OversizedProviderResponseIsRejected()
    {
        using var client = new GroqClient(new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(new string('x', 300_000)) })));
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.TranscribeAsync(new byte[] { 1, 2 }, "test-key", "whisper-large-v3", "auto", "", CancellationToken.None));
    }
    [Fact]
    public async Task UnexpectedFormatterFinishIsRejected()
    {
        using var client = new GroqClient(new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"choices\":[{\"finish_reason\":\"tool_calls\",\"message\":{\"content\":\"danger\"}}]}") })));
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.PolishAsync("speech", "test-key", CancellationToken.None));
    }
    [Fact]
    public async Task TruncatedFormatterOutputIsRejected()
    {
        using var client = new GroqClient(new StubHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"choices\":[{\"finish_reason\":\"length\",\"message\":{\"content\":\"incomplete\"}}]}") })));
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.PolishAsync("some speech", "test-key", CancellationToken.None));
    }
    [Fact]
    public async Task FullWavUploadPreservesCapturedPcmExactly()
    {
        byte[] pcm = { 1, 2, 3, 4, 250, 255 };
        byte[] wav = WavEncoder.Encode16KhzMono(pcm);
        using var client = new GroqClient(new StubHandler(async (request, token) =>
        {
            var multipart = Assert.IsType<MultipartFormDataContent>(request.Content);
            var file = multipart.Single(part => part.Headers.ContentDisposition?.Name?.Trim('"') == "file");
            Assert.Equal("audio/wav", file.Headers.ContentType!.MediaType);
            Assert.Equal(wav, await file.ReadAsByteArrayAsync(token));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"text\":\"test\"}") };
        }));
        Assert.Equal("test", await client.TranscribeAsync(wav, "test-key", "whisper-large-v3", "en", "", CancellationToken.None));
    }

}
