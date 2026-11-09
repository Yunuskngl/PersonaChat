using Microsoft.EntityFrameworkCore;
using PersonaChat.Core.Models;
using PersonaChat.Core.Services;
using PersonaChat.Core.Storage;

namespace PersonaChat.Tests;

public sealed class PracticeTests : IDisposable
{
    private readonly TestApp _app = new();
    private static CancellationToken Ct => CancellationToken.None;

    private const string LongText = "This text is long enough to be reviewed by the teacher.";

    public void Dispose() => _app.Dispose();

    [Fact]
    public void RealCatalogs_MeetTheProductTargets()
    {
        var data = new DataPaths(FindDataFolder());

        var scenarios = new ScenarioRepository(data);

        Assert.True(scenarios.GetSimulations().Count >= 100);
        Assert.True(scenarios.GetEmails().Count >= 100);
        Assert.True(new PersonaRepository(data).GetAll().Count >= 100);
    }

    [Fact]
    public async Task Simulation_StartsWithOpeningMessage()
    {
        var session = await _app.Simulations().StartAsync(TestApp.SimulationId, Ct);

        var opening = Assert.Single(session.Messages);
        Assert.Equal(ChatRole.Assistant, opening.Role);
    }

    [Fact]
    public async Task Simulation_ResumesAnOpenSession()
    {
        var first = await _app.Simulations().StartAsync(TestApp.SimulationId, Ct);
        var second = await _app.Simulations().StartAsync(TestApp.SimulationId, Ct);

        Assert.Equal(first.SessionId, second.SessionId);
    }

    [Fact]
    public async Task Simulation_SendsScenarioInSystemPrompt()
    {
        var session = await _app.Simulations().StartAsync(TestApp.SimulationId, Ct);

        await _app.Simulations().SendAsync(session.SessionId, "Sorry about that", Ct);

        Assert.Contains("A late delivery", _app.Llm.Requests[^1].System);
        Assert.Equal(ChatRole.User, _app.Llm.Requests[^1].Messages[0].Role);
    }

    [Fact]
    public async Task Simulation_EndCreatesOneReportWithLearnerTextOnly()
    {
        var session = await _app.Simulations().StartAsync(TestApp.SimulationId, Ct);
        await _app.Simulations().SendAsync(session.SessionId, LongText, Ct);

        var reportId = await _app.Simulations().EndAsync(session.SessionId, Ct);
        var again = await _app.Simulations().EndAsync(session.SessionId, Ct);

        var report = await _app.Db.Reports.AsNoTracking().SingleAsync(r => r.Id == reportId, Ct);
        Assert.Null(again);
        Assert.Equal(LongText, report.SourceText);
    }

    [Fact]
    public async Task Simulation_RejectsMessagesAfterEnd()
    {
        var session = await _app.Simulations().StartAsync(TestApp.SimulationId, Ct);
        await _app.Simulations().EndAsync(session.SessionId, Ct);

        await Assert.ThrowsAsync<InvalidInputException>(() => _app.Simulations().SendAsync(session.SessionId, "Hi", Ct));
    }

    [Fact]
    public async Task Email_RejectsVeryShortText()
    {
        await Assert.ThrowsAsync<InvalidInputException>(() => _app.Emails().SubmitAsync(TestApp.EmailA, "Hi", "Too short", Ct));
    }

    [Fact]
    public async Task Email_SpinPrefersUnwrittenScenarios()
    {
        await _app.Emails().SubmitAsync(TestApp.EmailA, "Subject", LongText, Ct);

        for (var i = 0; i < 20; i++)
            Assert.Equal(TestApp.EmailB, (await _app.Emails().SpinAsync(Ct)).Scenario.Id);
    }

    [Fact]
    public async Task Chat_EndReportsOnlyNewMessages()
    {
        await _app.Chat().SendAsync(TestApp.PersonaA, LongText, ChatMode.Chat, Ct);
        var first = await _app.Chat().EndSessionAsync(TestApp.PersonaA, Ct);

        var nothingNew = await _app.Chat().EndSessionAsync(TestApp.PersonaA, Ct);
        await _app.Chat().SendAsync(TestApp.PersonaA, $"{LongText} Again.", ChatMode.Chat, Ct);
        var second = await _app.Chat().EndSessionAsync(TestApp.PersonaA, Ct);

        Assert.NotNull(first);
        Assert.Null(nothingNew);
        var report = await _app.Db.Reports.AsNoTracking().SingleAsync(r => r.Id == second, Ct);
        Assert.Equal($"{LongText} Again.", report.SourceText);
    }

    [Fact]
    public async Task Chat_ShortGreetingIsKeptForTheNextReport()
    {
        await _app.Chat().SendAsync(TestApp.PersonaA, "Hello", ChatMode.Chat, Ct);
        Assert.Null(await _app.Chat().EndSessionAsync(TestApp.PersonaA, Ct));

        await _app.Chat().SendAsync(TestApp.PersonaA, LongText, ChatMode.Chat, Ct);
        var reportId = await _app.Chat().EndSessionAsync(TestApp.PersonaA, Ct);

        var report = await _app.Db.Reports.AsNoTracking().SingleAsync(r => r.Id == reportId, Ct);
        Assert.StartsWith("Hello", report.SourceText);
    }

    [Fact]
    public async Task Report_FailsCleanlyAndCanBeRetried()
    {
        var id = await _app.Reports().CreateAsync(new ReportDraft(ReportKind.Chat, "T", null, null, LongText), Ct);
        _app.Llm.ShouldFail = true;

        await _app.Reports().GenerateAsync(id, Ct);
        Assert.Equal(ReportStatus.Failed, (await _app.Reports().GetAsync(id, Ct)).Status);

        _app.Llm.ShouldFail = false;
        _app.Llm.Reply = """{"summary":"Nice.","level":"B1","mistakes":[],"phrasing":[],"strengths":[],"nextSteps":[],"rewrite":""}""";
        await _app.Reports().RetryAsync(id, Ct);
        await _app.Reports().GenerateAsync(id, Ct);

        var report = await _app.Reports().GetAsync(id, Ct);
        Assert.Equal(ReportStatus.Ready, report.Status);
        Assert.Equal("Nice.", report.Content!.Summary);
    }

    [Theory]
    [InlineData(0, "just met")]
    [InlineData(4, "just met")]
    [InlineData(6, "getting to know")]
    [InlineData(24, "you are friends")]
    public async Task Chat_RelationshipGrowsWithMessageCount(int existing, string expected)
    {
        var conversation = new ConversationEntity { PersonaId = TestApp.PersonaA };
        _app.Db.Conversations.Add(conversation);
        await _app.Db.SaveChangesAsync(Ct);
        _app.Db.Messages.AddRange(Enumerable.Range(0, existing).Select(_ =>
            new MessageEntity { ConversationId = conversation.Id, Role = ChatRole.User, Content = "x" }));
        await _app.Db.SaveChangesAsync(Ct);

        await _app.Chat().SendAsync(TestApp.PersonaA, "Hi", ChatMode.Chat, Ct);

        Assert.Contains(expected, _app.Llm.Requests[^1].System);
    }

    private static string FindDataFolder()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "PersonaChat.slnx")))
            folder = folder.Parent;

        return Path.Combine(folder!.FullName, "src", "PersonaChat.Web", "Data");
    }
}
