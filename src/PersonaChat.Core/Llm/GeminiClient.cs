using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PersonaChat.Core.Models;

namespace PersonaChat.Core.Llm;

public sealed class GeminiClient(HttpClient http, IOptions<LlmOptions> options, ILogger<GeminiClient> logger)
    : HttpLlmClient(http, logger)
{
    private const string ApiKeyHeader = "x-goog-api-key";
    private const string UserRole = "user";
    private const string ModelRole = "model";
    private const string JsonMimeType = "application/json";

    private readonly CloudLlmOptions _options = options.Value.Gemini;

    protected override string ProviderName => "Gemini";

    protected override HttpRequestMessage BuildRequest(LlmRequest request)
    {
        var url = $"{_options.Url.TrimEnd('/')}/{Uri.EscapeDataString(_options.Model)}:generateContent";
        var message = PostJson(url, BuildBody(request));
        message.Headers.Add(ApiKeyHeader, _options.ApiKey);
        return message;
    }

    protected override async Task<string?> ReadTextAsync(HttpContent content, CancellationToken ct)
    {
        var body = await content.ReadFromJsonAsync<GeminiReply>(ct);
        var parts = body?.Candidates?.FirstOrDefault()?.Content?.Parts;
        return parts is null ? null : string.Concat(parts.Select(part => part.Text));
    }

    private GeminiBody BuildBody(LlmRequest request) => new(
        new Content(null, [new Part(request.System)]),
        request.Messages.Select(ToContent).ToList(),
        new GenerationConfig(_options.MaxTokens, request.JsonSchema is null ? null : JsonMimeType));

    private static Content ToContent(ChatMessage message) =>
        new(message.Role == ChatRole.User ? UserRole : ModelRole, [new Part(message.Content)]);

    private sealed record GeminiBody(
        [property: JsonPropertyName("system_instruction")] Content SystemInstruction,
        [property: JsonPropertyName("contents")] IReadOnlyList<Content> Contents,
        [property: JsonPropertyName("generationConfig")] GenerationConfig GenerationConfig);

    private sealed record Content(
        [property: JsonPropertyName("role"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Role,
        [property: JsonPropertyName("parts")] IReadOnlyList<Part>? Parts);

    private sealed record Part([property: JsonPropertyName("text")] string? Text);

    private sealed record GenerationConfig(
        [property: JsonPropertyName("maxOutputTokens")] int MaxOutputTokens,
        [property: JsonPropertyName("responseMimeType"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ResponseMimeType);

    private sealed record GeminiReply([property: JsonPropertyName("candidates")] IReadOnlyList<Candidate>? Candidates);

    private sealed record Candidate([property: JsonPropertyName("content")] Content? Content);
}
