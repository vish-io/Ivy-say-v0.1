using System.IO;
using System.Text;
using System.Text.Json;

namespace IvySay;

// Parses bounded OpenAI-compatible server-sent events; never treats partial output as complete.
internal static class StreamingCompletion
{
    private const int MaxStreamChars = 256 * 1024;
    private const int MaxEventChars = 64 * 1024;
    private const int MaxOutputChars = 64 * 1024;
    public static async Task<string> ReadAsync(Stream stream, Func<string, Task> onDelta, CancellationToken token)
    {
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), false, 4096, leaveOpen: true);
        var line = new StringBuilder();
        var data = new StringBuilder();
        var output = new StringBuilder();
        bool finished = false, done = false;
        int total = 0;
        async Task DispatchAsync()
        {
            if (data.Length == 0) return;
            string payload = data.ToString(); data.Clear();
            if (payload == "[DONE]") { done = true; return; }
            if (done) throw new InvalidOperationException("Unexpected data after stream completion.");
            using var json = JsonDocument.Parse(payload);
            var root = json.RootElement;
            if (root.TryGetProperty("error", out _)) throw new InvalidOperationException("Formatting stream returned an error.");
            if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array) throw new InvalidOperationException("Invalid formatting stream.");
            if (choices.GetArrayLength() == 0) return;
            if (finished) throw new InvalidOperationException("Unexpected data after stream completion.");
            var choice = choices[0];
            if (choice.TryGetProperty("delta", out var delta) && delta.ValueKind == JsonValueKind.Object && delta.TryGetProperty("content", out var content) && content.ValueKind != JsonValueKind.Null)
            {
                if (content.ValueKind != JsonValueKind.String) throw new InvalidOperationException("Invalid formatting stream content.");
                string fragment = content.GetString() ?? "";
                if (output.Length + fragment.Length > MaxOutputChars) throw new InvalidOperationException("Formatting output was too large.");
                if (fragment.Length > 0) { output.Append(fragment); await onDelta(fragment); }
            }
            if (choice.TryGetProperty("finish_reason", out var reason) && reason.ValueKind != JsonValueKind.Null)
            {
                if (reason.ValueKind != JsonValueKind.String || reason.GetString() != "stop") throw new InvalidOperationException("Formatting did not complete.");
                finished = true;
            }
        }
        async Task ProcessLineAsync()
        {
            string value = line.ToString().TrimEnd('\r'); line.Clear();
            if (value.Length == 0) { await DispatchAsync(); return; }
            if (value.StartsWith("data:", StringComparison.Ordinal))
            {
                string part = value[5..];
                if (part.StartsWith(' ')) part = part[1..];
                if (data.Length + part.Length + 1 > MaxEventChars) throw new InvalidOperationException("Formatting event was too large.");
                if (data.Length > 0) data.Append('\n');
                data.Append(part);
            }
        }
        char[] buffer = new char[4096];
        while (!done)
        {
            int count = await reader.ReadAsync(buffer.AsMemory(), token);
            if (count == 0) break;
            total += count;
            if (total > MaxStreamChars) throw new InvalidOperationException("Formatting stream was too large.");
            for (int i = 0; i < count && !done; i++)
            {
                if (buffer[i] == '\n') await ProcessLineAsync();
                else { if (line.Length >= MaxEventChars) throw new InvalidOperationException("Formatting event was too large."); line.Append(buffer[i]); }
            }
        }
        if (!done) throw new IOException("Formatting stream was interrupted.");
        if (!finished || string.IsNullOrWhiteSpace(output.ToString())) throw new InvalidOperationException("Formatting stream ended before completion.");
        return output.ToString();
    }
}
