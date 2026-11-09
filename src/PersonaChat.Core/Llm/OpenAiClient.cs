using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PersonaChat.Core.Llm;

/// <summary>Uses the Responses API, the only OpenAI endpoint that serves Codex models.</summary>
public sealed class OpenAiClient(HttpClient http, IOptions<LlmOptions> options, ILogger<OpenAiClient> logger)
    : HttpLlmClient(http, logger)
{
    private const string BearerScheme = "Bearer";
    private const string JsonObjectFormat = "json_object";
    private const string OutputTextType = "output_text";

    private readonly CloudLlmOptions _options = options.Value.OpenAi;

    protected override string ProviderName => "OpenAI";

    protected override HttpRequestMessage BuildRequest(LlmRequest request)
    {
        var message = PostJson(_options.Url, BuildBody(request));
        message.Headers.Authorization = new AuthenticationHeaderValue(BearerScheme, _options.ApiKey);
        return message;
    }

    protected override async Task<string?> ReadTextAsync(HttpContent content, CancellationToken ct)
    {
        var body = await content.ReadFromJsonAsync<ResponsesReply>(ct);
        var parts = body?.Output?.SelectMany(item => item.Content ?? []).Where(part => part.Type == OutputTextType);
        return parts is null ? null : string.Concat(parts.Select(part => part.Text));
    }

    private ResponsesBody BuildBody(LlmRequest request) => new(
        _options.Model,
        request.System,
        request.Messages.Select(m => new InputMessage(LlmRoles.Of(m.Role), m.Content)).ToList(),
        _options.MaxTokens,
        request.JsonSchema is null ? null : new TextOptions(new TextFormat(JsonObjectFormat)));

    private sealed record ResponsesBody(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("instructions")] string Instructions,
        [property: JsonPropertyName("input")] IReadOnlyList<InputMessage> Input,
        [property: JsonPropertyName("max_output_tokens")] int MaxOutputTokens,
        [property: JsonPropertyName("text"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] TextOptions? Text);

    private sealed record InputMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record TextOptions([property: JsonPropertyName("format")] TextFormat Format);

    private sealed record TextFormat([property: JsonPropertyName("type")] string Type);

    private sealed record ResponsesReply([property: JsonPropertyName("output")] IReadOnlyList<OutputItem>? Output);

    private sealed record OutputItem([property: JsonPropertyName("content")] IReadOnlyList<OutputPart>? Content);

    private sealed record OutputPart(
        [property: JsonPropertyName("type")] string? Type,
        [property: JsonPropertyName("text")] string? Text);
}
