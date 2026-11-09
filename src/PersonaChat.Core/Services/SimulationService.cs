using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PersonaChat.Core.Llm;
using PersonaChat.Core.Models;
using PersonaChat.Core.Storage;

namespace PersonaChat.Core.Services;

public sealed class SimulationService(
    AppDbContext db,
    ILlmClient llm,
    ScenarioRepository scenarios,
    PromptBuilder prompts,
    ReportService reports,
    ConversationGate gate,
    IOptions<ChatOptions> chatOptions,
    IOptions<MemoryOptions> memoryOptions)
{
    private readonly ChatOptions _chat = chatOptions.Value;
    private readonly MemoryOptions _memory = memoryOptions.Value;

    public Task<SimulationSessionView> StartAsync(string scenarioId, CancellationToken ct)
    {
        var scenario = scenarios.RequireSimulation(scenarioId);
        return gate.RunAsync(() => FindOrCreateSessionAsync(scenario, ct), ct);
    }

    public Task<string> SendAsync(int sessionId, string? text, CancellationToken ct)
    {
        var cleaned = TextSanitizer.CleanInput(text, _chat.MaxInputLength);
        return gate.RunAsync(() => RunTurnAsync(sessionId, cleaned, ct), ct);
    }

    /// <summary>Returns the new report id, or null when there was too little text to review.</summary>
    public Task<int?> EndAsync(int sessionId, CancellationToken ct) =>
        gate.RunAsync(() => EndSessionAsync(sessionId, ct), ct);

    private async Task<SimulationSessionView> FindOrCreateSessionAsync(SimulationScenario scenario, CancellationToken ct)
    {
        var open = await db.SimulationSessions.FirstOrDefaultAsync(s => s.ScenarioId == scenario.Id && s.EndedAt == null, ct);
        var session = open ?? await CreateSessionAsync(scenario, ct);
        return new SimulationSessionView(session.Id, await LoadMessagesAsync(session.Id, _chat.MaxDisplayMessages, ct));
    }

    private async Task<SimulationSessionEntity> CreateSessionAsync(SimulationScenario scenario, CancellationToken ct)
    {
        var session = new SimulationSessionEntity { ScenarioId = scenario.Id, CreatedAt = DateTime.UtcNow };
        db.SimulationSessions.Add(session);
        await db.SaveChangesAsync(ct);

        db.SimulationMessages.Add(NewMessage(session.Id, ChatRole.Assistant, scenario.Opening));
        await db.SaveChangesAsync(ct);
        return session;
    }

    private async Task<string> RunTurnAsync(int sessionId, string text, CancellationToken ct)
    {
        var session = await RequireOpenAsync(sessionId, ct);
        var scenario = scenarios.RequireSimulation(session.ScenarioId);
        var recent = await LoadMessagesAsync(sessionId, _memory.ShortTermMessages, ct);

        var messages = prompts.TrimHistory([.. recent, new ChatMessage(ChatRole.User, text)], _memory.ShortTermMessages);
        var reply = await llm.SendAsync(new LlmRequest(prompts.BuildSimulation(scenario), messages), ct);

        db.SimulationMessages.AddRange(NewMessage(sessionId, ChatRole.User, text), NewMessage(sessionId, ChatRole.Assistant, reply));
        await db.SaveChangesAsync(ct);
        return reply;
    }

    private async Task<int?> EndSessionAsync(int sessionId, CancellationToken ct)
    {
        var session = await db.SimulationSessions.FirstOrDefaultAsync(s => s.Id == sessionId, ct)
            ?? throw new ResourceNotFoundException("This simulation does not exist.");
        if (session.EndedAt is not null)
            return null;

        session.EndedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var learnerText = await LoadLearnerTextAsync(sessionId, ct);
        if (!reports.HasEnoughText(learnerText))
            return null;

        var scenario = scenarios.RequireSimulation(session.ScenarioId);
        var context = $"{scenario.Situation} The other person is {scenario.Counterpart}. Goal: {scenario.Goal}";
        return await reports.CreateAsync(new ReportDraft(ReportKind.Simulation, scenario.Title, scenario.Id, context, learnerText), ct);
    }

    private async Task<SimulationSessionEntity> RequireOpenAsync(int sessionId, CancellationToken ct)
    {
        var session = await db.SimulationSessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sessionId, ct)
            ?? throw new ResourceNotFoundException("This simulation does not exist.");
        return session.EndedAt is null ? session : throw new InvalidInputException("This simulation has ended.");
    }

    private async Task<IReadOnlyList<ChatMessage>> LoadMessagesAsync(int sessionId, int count, CancellationToken ct)
    {
        var newestFirst = await db.SimulationMessages.AsNoTracking()
            .Where(m => m.SessionId == sessionId)
            .OrderByDescending(m => m.Id)
            .Take(count)
            .Select(m => new ChatMessage(m.Role, m.Content))
            .ToListAsync(ct);

        newestFirst.Reverse();
        return newestFirst;
    }

    private async Task<string> LoadLearnerTextAsync(int sessionId, CancellationToken ct)
    {
        var lines = await db.SimulationMessages.AsNoTracking()
            .Where(m => m.SessionId == sessionId && m.Role == ChatRole.User)
            .OrderBy(m => m.Id)
            .Select(m => m.Content)
            .ToListAsync(ct);

        return string.Join('\n', lines);
    }

    private static SimulationMessageEntity NewMessage(int sessionId, ChatRole role, string content) =>
        new() { SessionId = sessionId, Role = role, Content = content, CreatedAt = DateTime.UtcNow };
}
