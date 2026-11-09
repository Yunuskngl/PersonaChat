using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PersonaChat.Core.Models;
using PersonaChat.Core.Storage;

namespace PersonaChat.Core.Services;

public sealed class EmailPracticeService(
    AppDbContext db,
    ScenarioRepository scenarios,
    ReportService reports,
    IOptions<ReportOptions> options)
{
    private const int MaxSubjectLength = 150;

    private readonly ReportOptions _options = options.Value;

    /// <summary>Picks a random scenario, preferring ones the learner has not written yet.</summary>
    public async Task<EmailSpin> SpinAsync(CancellationToken ct)
    {
        var all = scenarios.GetEmails();
        var done = await db.Reports.AsNoTracking()
            .Where(r => r.Kind == ReportKind.Email && r.ScenarioId != null)
            .Select(r => r.ScenarioId!)
            .ToListAsync(ct);

        var fresh = all.Where(s => !done.Contains(s.Id)).ToList();
        var pool = fresh.Count > 0 ? fresh : all;
        var scenario = pool[Random.Shared.Next(pool.Count)];

        return new EmailSpin(IndexOf(all, scenario), all.Count, scenario);
    }

    public Task<int> SubmitAsync(string scenarioId, string? subject, string? body, CancellationToken ct)
    {
        var scenario = scenarios.RequireEmail(scenarioId);
        var cleanSubject = TextSanitizer.CleanInput(subject, MaxSubjectLength);
        var cleanBody = TextSanitizer.CleanInput(body, _options.MaxEmailLength);
        if (!reports.HasEnoughText(cleanBody))
            throw new InvalidInputException("Your email is very short. Please write a little more.");

        var context = $"{scenario.Situation} Task: {scenario.Task} The email is addressed to {scenario.To}.";
        var source = $"Subject: {cleanSubject}\n\n{cleanBody}";
        return reports.CreateAsync(new ReportDraft(ReportKind.Email, scenario.Title, scenario.Id, context, source), ct);
    }

    private static int IndexOf(IReadOnlyList<EmailScenario> all, EmailScenario scenario)
    {
        for (var i = 0; i < all.Count; i++)
        {
            if (all[i].Id == scenario.Id)
                return i;
        }

        return 0;
    }
}
