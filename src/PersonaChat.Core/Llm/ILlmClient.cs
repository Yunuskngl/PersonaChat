using PersonaChat.Core.Models;

namespace PersonaChat.Core.Llm;

/// <param name="JsonSchema">When set, the reply must be a JSON object of this shape.</param>
/// <param name="LongReply">Allows more output on providers with a small token budget.</param>
public sealed record LlmRequest(
    string System,
    IReadOnlyList<ChatMessage> Messages,
    string? JsonSchema = null,
    bool LongReply = false);

/// <summary>Sends a conversation to a language model, whichever provider is configured.</summary>
public interface ILlmClient
{
    /// <summary>Returns the model's reply text.</summary>
    /// <exception cref="LlmException">The provider failed or returned no usable reply.</exception>
    Task<string> SendAsync(LlmRequest request, CancellationToken ct);
}
