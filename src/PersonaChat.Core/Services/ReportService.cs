using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PersonaChat.Core.Llm;
using PersonaChat.Core.Models;
using PersonaChat.Core.Storage;

namespace PersonaChat.Core.Services;

public sealed partial class ReportService(
    AppDbContext db,
    ReportAnalyzer analyzer,
    ReportQueue queue,
    IOptions<ReportOptions> options,
    ILogger<ReportService> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ReportOptions _options = options.Value;

    [GeneratedRegex(@"\S+")]
    private static partial Regex WordToken();

    public bool HasEnoughText(string text) => WordToken().Count(text) >= _options.MinWords;

    public async Task<int> CreateAsync(ReportDraft draft, CancellationToken ct)
    {
        var report = new ReportEntity
        {
            Kind = draft.Kind,
            Title = draft.Title,
            ScenarioId = draft.ScenarioId,
            Context = draft.Context,
            SourceText = draft.Source,
            Status = ReportStatus.Pending,
            CreatedAt = DateTime.UtcNow,
        };

        db.Reports.Add(report);
        await db.SaveChangesAsync(ct);
        queue.Enqueue(report.Id);
        return report.Id;
    }

    public async Task<IReadOnlyList<ReportSummary>> ListAsync(CancellationToken ct)
    {
        var rows = await db.Reports.AsNoTracking()
            .OrderByDescending(r => r.Id)
            .Select(r => new { r.Id, r.Kind, r.Title, r.Status, r.CreatedAt })
            .ToListAsync(ct);

        return rows.Select(r => new ReportSummary(r.Id, r.Kind, r.Title, r.Status, AsUtc(r.CreatedAt))).ToList();
    }

    public async Task<ReportDetails> GetAsync(int id, CancellationToken ct)
    {
        var report = await RequireAsync(id, ct);
        var content = report.ResultJson is null ? null : JsonSerializer.Deserialize<ReportContent>(report.ResultJson, JsonOptions);
        return new ReportDetails(report.Id, report.Kind, report.Title, report.Status, AsUtc(report.CreatedAt), content);
    }

    public async Task RetryAsync(int id, CancellationToken ct)
    {
        var report = await RequireAsync(id, ct);
        if (report.Status != ReportStatus.Failed)
            return;

        report.Status = ReportStatus.Pending;
        await db.SaveChangesAsync(ct);
        queue.Enqueue(report.Id);
    }

    public async Task GenerateAsync(int id, CancellationToken ct)
    {
        var report = await RequireAsync(id, ct);
        if (report.Status != ReportStatus.Pending)
            return;

        try
        {
            var content = await analyzer.AnalyzeAsync(report.Kind, report.Context, report.SourceText, ct);
            report.ResultJson = JsonSerializer.Serialize(content, JsonOptions);
            report.Status = ReportStatus.Ready;
        }
        catch (LlmException ex)
        {
            logger.LogWarning(ex, "Report {Id} failed", id);
            report.Status = ReportStatus.Failed;
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Picks up reports that were interrupted by a restart.</summary>
    public async Task RequeuePendingAsync(CancellationToken ct)
    {
        var ids = await db.Reports.AsNoTracking()
            .Where(r => r.Status == ReportStatus.Pending)
            .Select(r => r.Id)
            .ToListAsync(ct);

        ids.ForEach(queue.Enqueue);
    }

    public Task<bool> HasPendingAsync(CancellationToken ct) =>
        db.Reports.AsNoTracking().AnyAsync(r => r.Status == ReportStatus.Pending, ct);

    private async Task<ReportEntity> RequireAsync(int id, CancellationToken ct) =>
        await db.Reports.FirstOrDefaultAsync(r => r.Id == id, ct)
        ?? throw new ResourceNotFoundException("This report does not exist.");

    // SQLite stores no time zone, but every value is written as UTC.
    private static DateTime AsUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
