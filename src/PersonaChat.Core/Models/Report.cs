namespace PersonaChat.Core.Models;

public enum ReportKind
{
    Chat,
    Simulation,
    Email,
}

public enum ReportStatus
{
    Pending,
    Ready,
    Failed,
}

public sealed record ReportDraft(ReportKind Kind, string Title, string? ScenarioId, string? Context, string Source);

public sealed record ReportSummary(int Id, ReportKind Kind, string Title, ReportStatus Status, DateTime CreatedAt);

public sealed record ReportDetails(int Id, ReportKind Kind, string Title, ReportStatus Status, DateTime CreatedAt, ReportContent? Content);

public sealed record ReportContent(
    string Summary,
    string Level,
    int TotalWords,
    IReadOnlyList<Mistake> Mistakes,
    IReadOnlyList<NaturalPhrasing> Phrasing,
    IReadOnlyList<WordUse> Words,
    IReadOnlyList<string> Strengths,
    IReadOnlyList<string> NextSteps,
    string? Rewrite);

public sealed record Mistake(string Original, string Corrected, string Type, string Explanation);

public sealed record NaturalPhrasing(string Original, string Natural, string Why);

public sealed record WordUse(string Word, int Count, bool Correct);
