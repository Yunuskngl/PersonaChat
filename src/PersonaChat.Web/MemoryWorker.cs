using PersonaChat.Core.Llm;
using PersonaChat.Core.Services;

namespace PersonaChat.Web;

public sealed class MemoryWorker(
    MemoryQueue queue,
    IServiceScopeFactory scopes,
    ILogger<MemoryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var personaId in queue.ReadAllAsync(stoppingToken))
            await ExtractAsync(personaId, stoppingToken);
    }

    private async Task ExtractAsync(string personaId, CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<MemoryService>().ExtractIfDueAsync(personaId, ct);
            logger.LogInformation("Memory check finished for {PersonaId}", personaId);
        }
        catch (LlmException ex)
        {
            // The counter is not reset on failure, so the next turn queues this again.
            logger.LogWarning(ex, "Memory extraction failed for {PersonaId}", personaId);
        }
    }
}
