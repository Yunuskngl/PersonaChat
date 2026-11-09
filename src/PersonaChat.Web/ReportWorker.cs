using PersonaChat.Core.Services;

namespace PersonaChat.Web;

public sealed class ReportWorker(
    ReportQueue queue,
    IServiceScopeFactory scopes,
    ILogger<ReportWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RequeuePendingAsync(stoppingToken);

        await foreach (var reportId in queue.ReadAllAsync(stoppingToken))
            await GenerateAsync(reportId, stoppingToken);
    }

    private async Task RequeuePendingAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ReportService>().RequeuePendingAsync(ct);
    }

    private async Task GenerateAsync(int reportId, CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<ReportService>().GenerateAsync(reportId, ct);
            logger.LogInformation("Report {Id} processed", reportId);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // One bad report must not stop the worker.
            logger.LogError(ex, "Report {Id} crashed", reportId);
        }
    }
}
