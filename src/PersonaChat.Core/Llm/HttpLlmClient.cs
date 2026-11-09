using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace PersonaChat.Core.Llm;

/// <summary>Shared error handling for providers that are called over plain HTTP.</summary>
public abstract class HttpLlmClient(HttpClient http, ILogger logger) : ILlmClient
{
    private const string TimeoutMessage = "The AI model took too long to answer. Please try again.";

    protected abstract string ProviderName { get; }

    protected virtual string UnavailableMessage => "The AI service is not available right now. Please try again in a moment.";

    protected abstract HttpRequestMessage BuildRequest(LlmRequest request);

    protected abstract Task<string?> ReadTextAsync(HttpContent content, CancellationToken ct);

    public async Task<string> SendAsync(LlmRequest request, CancellationToken ct)
    {
        try
        {
            using var message = BuildRequest(request);
            using var response = await http.SendAsync(message, ct);
            return await ReadReplyAsync(response, ct);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "Could not reach {Provider}", ProviderName);
            throw new LlmException(UnavailableMessage, ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning("{Provider} timed out", ProviderName);
            throw new LlmException(TimeoutMessage, ex);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "{Provider} returned invalid JSON", ProviderName);
            throw new LlmException(UnavailableMessage, ex);
        }
    }

    protected static HttpRequestMessage PostJson<T>(string url, T body) =>
        new(HttpMethod.Post, url) { Content = JsonContent.Create(body) };

    private async Task<string> ReadReplyAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("{Provider} returned status {Status}", ProviderName, (int)response.StatusCode);
            throw new LlmException(UnavailableMessage);
        }

        var text = (await ReadTextAsync(response.Content, ct))?.Trim();
        if (!string.IsNullOrEmpty(text))
            return text;

        logger.LogError("{Provider} returned an empty reply", ProviderName);
        throw new LlmException(UnavailableMessage);
    }
}
