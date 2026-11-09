using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PersonaChat.Core.Models;

namespace PersonaChat.Core.Llm;

public sealed class OllamaClient(HttpClient http, IOptions<LlmOptions> options, ILogger<OllamaClient> logger)
    : HttpLlmClient(http, logger)
{
    private readonly OllamaOptions _options = options.Value.Ollama;

    protected override string ProviderName => "Ollama";

    protected override string UnavailableMessage => "The local AI model is not responding. Please check that Ollama is running.";

    protected override HttpRequestMessage BuildRequest(LlmRequest request) => PostJson(_options.Url, BuildBody(request));

    protected override async Task<string?> ReadTextAsync(HttpContent content, CancellationToken ct)
    {
        var body = await content.ReadFromJsonAsync<OllamaResponse>(ct);
        return body?.Message?.Content;
    }

    private OllamaRequest BuildBody(LlmRequest request)
    {
        var messages = new List<OllamaMessage> { new(LlmRoles.System, request.System) };
        messages.AddRange(request.Messages.Select(m => new OllamaMessage(LlmRoles.Of(m.Role), m.Content)));

        var maxTokens = request.LongReply ? _options.MaxLongReplyTokens : _options.MaxReplyTokens;
        var format = request.JsonSchema is null ? (JsonElement?)null : JsonSerializer.Deserialize<JsonElement>(request.JsonSchema);
        return new OllamaRequest(_options.Model, messages, false, format, new OllamaRunOptions(_options.NumCtx, maxTokens));
    }

    private sealed record OllamaRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] IReadOnlyList<OllamaMessage> Messages,
        [property: JsonPropertyName("stream")] bool Stream,
        [property: JsonPropertyName("format"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] JsonElement? Format,
        [property: JsonPropertyName("options")] OllamaRunOptions Options);

    private sealed record OllamaMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed record OllamaRunOptions(
        [property: JsonPropertyName("num_ctx")] int NumCtx,
        [property: JsonPropertyName("num_predict")] int NumPredict);

    private sealed record OllamaResponse(
        [property: JsonPropertyName("message")] OllamaMessage? Message);
}
