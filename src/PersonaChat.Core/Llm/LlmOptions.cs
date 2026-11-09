using Microsoft.Extensions.Options;

namespace PersonaChat.Core.Llm;

public static class LlmProviders
{
    public const string Ollama = "Ollama";
    public const string Claude = "Claude";
    public const string OpenAi = "OpenAI";
    public const string Gemini = "Gemini";
    public const string OpenAiCompatible = "OpenAICompatible";
}

public sealed class LlmOptions
{
    public const string Section = "Llm";

    public string Provider { get; set; } = LlmProviders.Ollama;
    public OllamaOptions Ollama { get; set; } = new();
    public ClaudeOptions Claude { get; set; } = new();
    public CloudLlmOptions OpenAi { get; set; } = new() { Url = "https://api.openai.com/v1/responses" };
    public CloudLlmOptions Gemini { get; set; } = new() { Url = "https://generativelanguage.googleapis.com/v1beta/models" };
    public CloudLlmOptions OpenAiCompatible { get; set; } = new();
}

public sealed class OllamaOptions
{
    public string Url { get; set; } = "http://localhost:11434/api/chat";
    public string Model { get; set; } = "gemma3:4b";
    public int NumCtx { get; set; } = 4096;
    public int MaxReplyTokens { get; set; } = 600;
    public int MaxLongReplyTokens { get; set; } = 2000;
    public int TimeoutSeconds { get; set; } = 300;
}

public class CloudLlmOptions
{
    public string? ApiKey { get; set; }
    public string Model { get; set; } = "";
    public string Url { get; set; } = "";
    public int MaxTokens { get; set; } = 16000;
    public int TimeoutSeconds { get; set; } = 120;
}

public sealed class ClaudeOptions : CloudLlmOptions
{
    public ClaudeOptions() => Model = "claude-opus-5-5";
}

/// <summary>Checks only the selected provider, so unused providers need no keys.</summary>
public sealed class LlmOptionsValidator : IValidateOptions<LlmOptions>
{
    public ValidateOptionsResult Validate(string? name, LlmOptions options) => options.Provider switch
    {
        LlmProviders.Ollama => RequireUrl(options.Ollama.Url, "Ollama"),
        LlmProviders.Claude => RequireKeyAndModel(options.Claude, "Claude"),
        LlmProviders.OpenAi => RequireKeyAndModel(options.OpenAi, "OpenAi"),
        LlmProviders.Gemini => RequireKeyAndModel(options.Gemini, "Gemini"),
        LlmProviders.OpenAiCompatible => RequireCompatible(options.OpenAiCompatible),
        _ => ValidateOptionsResult.Fail(
            $"Llm:Provider must be one of: {LlmProviders.Ollama}, {LlmProviders.Claude}, {LlmProviders.OpenAi}, {LlmProviders.Gemini}, {LlmProviders.OpenAiCompatible}."),
    };

    private static ValidateOptionsResult RequireKeyAndModel(CloudLlmOptions options, string section)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey))
            return ValidateOptionsResult.Fail($"Llm:{section}:ApiKey is missing. Set it with dotnet user-secrets or an environment variable.");

        return RequireModel(options, section);
    }

    private static ValidateOptionsResult RequireCompatible(CloudLlmOptions options)
    {
        var url = RequireUrl(options.Url, "OpenAiCompatible");
        return url.Failed ? url : RequireModel(options, "OpenAiCompatible");
    }

    private static ValidateOptionsResult RequireModel(CloudLlmOptions options, string section) =>
        string.IsNullOrWhiteSpace(options.Model)
            ? ValidateOptionsResult.Fail($"Llm:{section}:Model is missing.")
            : ValidateOptionsResult.Success;

    private static ValidateOptionsResult RequireUrl(string url, string section) =>
        Uri.TryCreate(url, UriKind.Absolute, out _)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail($"Llm:{section}:Url must be an absolute URL.");
}
