using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PersonaChat.Core.Llm;

/// <summary>Any server with a chat/completions endpoint: OpenRouter, Groq, LM Studio, LiteLLM and others.</summary>
public sealed class OpenAiCompatibleClient(HttpClient http, IOptions<LlmOptions> options, ILogger<OpenAiCompatibleClient> logger)
    : HttpLlmClient(http, logger)
{
    private const string BearerScheme = "Bearer";
    private const string JsonObjectFormat = "json_object";

    private readonly CloudLlmOptions _options = options.Value.OpenAiCompatible;

    protected override string ProviderName => "OpenAI-compatible server";

    protected override HttpRequestMessage BuildRequest(LlmRequest request)
    {
        var message = PostJson(_options.Url, BuildBody(request));
        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            message.Headers.Authorization = new AuthenticationHeaderValue(BearerScheme, _options.ApiKey);
        return message;
    }

    protected override async Task<string?> ReadTextAsync(HttpContent content, CancellationToken ct)
    {
        var body = await content.ReadFromJsonAsync<CompletionReply>(ct);
        return body?.Choices?.FirstOrDefault()?.Message?.Content;
    }

    private CompletionBody BuildBody(LlmRequest request)
    {
        var messages = new List<Message> { new(LlmRoles.System, request.System) };
        messages.AddRange(request.Messages.Select(m => new Message(LlmRoles.Of(m.Role), m.Content)));

        var format = request.JsonSchema is null ? null : new ResponseFormat(JsonObjectFormat);
        return new CompletionBody(_options.Model, messages, _options.MaxTokens, format);
    }

    private sealed record CompletionBody(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<Message> Messages,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        [property: JsonPropertyName("response_format"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ResponseFormat? ResponseFormat);

    private sealed record Message(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string? Content);

    private sealed record ResponseFormat([property: JsonPropertyName("type")] string Type);

    private sealed record CompletionReply([property: JsonPropertyName("choices")] IReadOnlyList<Choice>? Choices);

    private sealed record Choice([property: JsonPropertyName("message")] Message? Message);
}
