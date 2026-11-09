using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PersonaChat.Core.Llm;
using PersonaChat.Core.Models;
using PersonaChat.Core.Services;
using PersonaChat.Core.Storage;

namespace PersonaChat.Tests;

internal sealed class FakeLlm : ILlmClient
{
    public List<LlmRequest> Requests { get; } = [];
    public string Reply { get; set; } = "Hello!";
    public bool ShouldFail { get; set; }

    public Task<string> SendAsync(LlmRequest request, CancellationToken ct)
    {
        Requests.Add(request);
        return ShouldFail ? throw new LlmException("offline") : Task.FromResult(Reply);
    }
}

/// <summary>A real SQLite database that lives in memory for one test.</summary>
internal sealed class TestApp : IDisposable
{
    public const string PersonaA = "persona-a";
    public const string PersonaB = "persona-b";
    public const string SimulationId = "sim-a";
    public const string EmailA = "email-a";
    public const string EmailB = "email-b";

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly string _folder = Directory.CreateTempSubdirectory().FullName;

    public FakeLlm Llm { get; } = new();
    public MemoryQueue Queue { get; } = new();
    public ReportQueue ReportQueue { get; } = new();
    public ConversationGate Gate { get; } = new();
    public AppDbContext Db { get; }
    public MemoryOptions MemoryOptions { get; } = new() { ShortTermMessages = 4, SummarizeEvery = 4, MaxNotes = 3, MaxNoteLength = 20 };

    public TestApp()
    {
        _connection.Open();
        Db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        Db.Database.EnsureCreated();
        Db.Personas.AddRange(
            new PersonaEntity { Id = PersonaA, Name = "A" },
            new PersonaEntity { Id = PersonaB, Name = "B" });
        Db.SaveChanges();

        var paths = new DataPaths(_folder);
        File.WriteAllText(paths.Personas, PersonasJson);
        File.WriteAllText(paths.Simulations, SimulationsJson);
        File.WriteAllText(paths.Emails, EmailsJson);
    }

    public MemoryService Memory() => new(Db, Llm, Gate, Options.Create(MemoryOptions));

    public ReportService Reports() => new(
        Db, new ReportAnalyzer(Llm, Options.Create(new ReportOptions())), ReportQueue,
        Options.Create(new ReportOptions()), NullLogger<ReportService>.Instance);

    public ChatService Chat() => new(
        Db, Llm, new PersonaRepository(new DataPaths(_folder)), new PromptBuilder(), Memory(), Queue, Reports(), Gate,
        Options.Create(new ChatOptions()), Options.Create(MemoryOptions));

    public SimulationService Simulations() => new(
        Db, Llm, new ScenarioRepository(new DataPaths(_folder)), new PromptBuilder(), Reports(), Gate,
        Options.Create(new ChatOptions()), Options.Create(MemoryOptions));

    public EmailPracticeService Emails() => new(
        Db, new ScenarioRepository(new DataPaths(_folder)), Reports(), Options.Create(new ReportOptions()));

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
        Directory.Delete(_folder, recursive: true);
    }

    private const string SimulationsJson = """
        [ { "id": "sim-a", "category": "C", "channel": "Teams", "title": "T", "counterpart": "A client", "situation": "A late delivery", "opening": "Where is it?", "goal": "Apologise" } ]
        """;

    private const string EmailsJson = """
        [
          { "id": "email-a", "category": "C", "title": "A", "to": "X", "situation": "S", "task": "T" },
          { "id": "email-b", "category": "C", "title": "B", "to": "X", "situation": "S", "task": "T" }
        ]
        """;

    private const string PersonasJson = """
        [
          { "id": "persona-a", "name": "A", "age": 30, "location": "X", "backstory": "b", "expertise": ["e"], "personality": "p", "speakingStyle": "s" },
          { "id": "persona-b", "name": "B", "age": 30, "location": "X", "backstory": "b", "expertise": ["e"], "personality": "p", "speakingStyle": "s" }
        ]
        """;
}

internal static class SamplePersona
{
    public static Persona Create() => new("elena", "Elena", 46, "Leiden", "An astronomer.", ["Stars"], "Curious.", "Calm.");
}
