using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PersonaChat.Core.Llm;
using PersonaChat.Core.Services;
using PersonaChat.Core.Storage;

namespace PersonaChat.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddPersonaChatCore(
        this IServiceCollection services, IConfiguration config, DataPaths paths)
    {
        var connection = new SqliteConnectionStringBuilder { DataSource = paths.Database }.ToString();

        services.AddSingleton(paths);
        services.AddDbContext<AppDbContext>(options => options.UseSqlite(connection));

        services.AddSingleton<PersonaRepository>();
        services.AddSingleton<PromptBuilder>();
        services.AddSingleton<ConversationGate>();
        services.AddSingleton<MemoryQueue>();
        services.AddSingleton<ReportQueue>();
        services.AddSingleton<ScenarioRepository>();
        services.AddScoped<MemoryService>();
        services.AddScoped<ChatService>();
        services.AddScoped<SimulationService>();
        services.AddScoped<EmailPracticeService>();
        services.AddScoped<ReportAnalyzer>();
        services.AddScoped<ReportService>();
        services.AddScoped<DatabaseInitializer>();

        AddServiceOptions(services, config);
        AddLlm(services, config);
        return services;
    }

    private static void AddServiceOptions(IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<ChatOptions>()
            .Bind(config.GetSection(ChatOptions.Section))
            .Validate(o => o is { MaxInputLength: > 0, MaxDisplayMessages: > 0, IntroMessages: >= 0 } && o.FriendMessages >= o.IntroMessages,
                "Chat limits must be positive, and FriendMessages must not be below IntroMessages.")
            .ValidateOnStart();

        services.AddOptions<MemoryOptions>()
            .Bind(config.GetSection(MemoryOptions.Section))
            .Validate(o => o is { ShortTermMessages: > 0, SummarizeEvery: > 0, MaxNotes: > 0, MaxNoteLength: > 0 },
                "Memory limits must be positive.")
            .ValidateOnStart();

        services.AddOptions<ReportOptions>()
            .Bind(config.GetSection(ReportOptions.Section))
            .Validate(o => o is { MinWords: > 0, MaxEmailLength: > 0, ChunkChars: > 0, MaxItemsPerChunk: > 0, MaxFieldLength: > 0, MaxRewriteLength: > 0 },
                "Report limits must be positive.")
            .ValidateOnStart();
    }

    private static void AddLlm(IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton<IValidateOptions<LlmOptions>, LlmOptionsValidator>();
        services.AddOptions<LlmOptions>().Bind(config.GetSection(LlmOptions.Section)).ValidateOnStart();

        AddHttpProvider<OllamaClient>(services, o => o.Ollama.TimeoutSeconds);
        AddHttpProvider<OpenAiClient>(services, o => o.OpenAi.TimeoutSeconds);
        AddHttpProvider<GeminiClient>(services, o => o.Gemini.TimeoutSeconds);
        AddHttpProvider<OpenAiCompatibleClient>(services, o => o.OpenAiCompatible.TimeoutSeconds);
        services.AddSingleton<ClaudeClient>();

        services.AddTransient<ILlmClient>(ResolveProvider);
    }

    private static void AddHttpProvider<TClient>(IServiceCollection services, Func<LlmOptions, int> timeoutSeconds)
        where TClient : class
    {
        services.AddHttpClient<TClient>((provider, http) =>
        {
            var options = provider.GetRequiredService<IOptions<LlmOptions>>().Value;
            http.Timeout = TimeSpan.FromSeconds(timeoutSeconds(options));
        });
    }

    private static ILlmClient ResolveProvider(IServiceProvider provider) =>
        provider.GetRequiredService<IOptions<LlmOptions>>().Value.Provider switch
        {
            LlmProviders.Claude => provider.GetRequiredService<ClaudeClient>(),
            LlmProviders.OpenAi => provider.GetRequiredService<OpenAiClient>(),
            LlmProviders.Gemini => provider.GetRequiredService<GeminiClient>(),
            LlmProviders.OpenAiCompatible => provider.GetRequiredService<OpenAiCompatibleClient>(),
            _ => provider.GetRequiredService<OllamaClient>(),
        };
}
