using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Beta.Messages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PersonaChat.Core.Models;

namespace PersonaChat.Core.Llm;

public sealed class ClaudeClient(IOptions<LlmOptions> options, ILogger<ClaudeClient> logger) : ILlmClient
{
    private const string FallbackBeta = "server-side-fallback-2026-07-01";
    private const string UnavailableMessage = "The AI service is not available right now. Please try again in a moment.";
    private const string DeclinedMessage = "Sorry, I can't answer that one. Let's talk about something else.";

    private readonly ClaudeOptions _options = options.Value.Claude;

    private readonly AnthropicClient _client = new()
    {
        ApiKey = options.Value.Claude.ApiKey,
        Timeout = TimeSpan.FromSeconds(options.Value.Claude.TimeoutSeconds),
    };

    public async Task<string> SendAsync(LlmRequest request, CancellationToken ct)
    {
        try
        {
            var response = await _client.Beta.Messages.Create(BuildParams(request), ct);
            return ReadReply(response);
        }
        catch (AnthropicException ex)
        {
            logger.LogError("Claude request failed with {Error}", ex.GetType().Name);
            throw new LlmException(UnavailableMessage, ex);
        }
    }

    private MessageCreateParams BuildParams(LlmRequest request) => new()
    {
        Model = _options.Model,
        MaxTokens = _options.MaxTokens,
        System = request.System,
        OutputConfig = BuildOutputConfig(request),
        Betas = [FallbackBeta],
        Fallbacks = new Default(),
        Messages = request.Messages.Select(ToClaude).ToList(),
    };

    private static BetaOutputConfig BuildOutputConfig(LlmRequest request)
    {
        if (request.JsonSchema is null)
            return new BetaOutputConfig { Effort = Effort.Low };

        var schema = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(request.JsonSchema)!;
        return new BetaOutputConfig { Effort = Effort.Medium, Format = new BetaJsonOutputFormat { Schema = schema } };
    }

    private static BetaMessageParam ToClaude(ChatMessage message)
    {
        if (message.Role == ChatRole.User)
            return new BetaUserMessageParam { Content = message.Content };

        return new BetaAssistantMessageParam { Content = message.Content };
    }

    private string ReadReply(BetaMessage response)
    {
        if (response.StopReason == BetaStopReason.Refusal)
            throw new LlmException(DeclinedMessage);

        var text = string.Concat(response.Content.Select(block => block.Value).OfType<BetaTextBlock>().Select(block => block.Text)).Trim();
        if (text.Length > 0)
            return text;

        logger.LogError("Claude returned an empty reply");
        throw new LlmException(UnavailableMessage);
    }
}
