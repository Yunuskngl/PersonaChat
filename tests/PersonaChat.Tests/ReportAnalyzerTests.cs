using System.Text.Json;
using Microsoft.Extensions.Options;
using PersonaChat.Core.Models;
using PersonaChat.Core.Services;

namespace PersonaChat.Tests;

public class ReportAnalyzerTests
{
    private readonly FakeLlm _llm = new();
    private readonly ReportOptions _options = new() { ChunkChars = 40 };

    private ReportAnalyzer Analyzer() => new(_llm, Options.Create(_options));

    private static string Review(object[]? mistakes = null, object[]? phrasing = null, string rewrite = "") =>
        JsonSerializer.Serialize(new
        {
            summary = "Good work.",
            level = "B1",
            mistakes = mistakes ?? [],
            phrasing = phrasing ?? [],
            strengths = new[] { "Clear ideas" },
            nextSteps = new[] { "Practise articles" },
            rewrite,
        });

    private static object Fix(string original, string corrected, string type = "grammar") =>
        new { original, corrected, type, explanation = "Because." };

    [Fact]
    public async Task Keeps_MistakesThatQuoteTheLearner()
    {
        _llm.Reply = Review([Fix("I am agree", "I agree")]);

        var report = await Analyzer().AnalyzeAsync(ReportKind.Chat, null, "I am agree with you", default);

        Assert.Equal("I agree", Assert.Single(report.Mistakes).Corrected);
    }

    [Fact]
    public async Task Drops_MistakesTheLearnerNeverWrote()
    {
        _llm.Reply = Review([Fix("She go home", "She goes home")]);

        var report = await Analyzer().AnalyzeAsync(ReportKind.Chat, null, "I am at work today", default);

        Assert.Empty(report.Mistakes);
    }

    [Fact]
    public async Task Ignores_CapitalsAndFullStopsInChat()
    {
        _llm.Reply = Review([Fix("i like tea", "I like tea.", "punctuation")]);

        var report = await Analyzer().AnalyzeAsync(ReportKind.Chat, null, "i like tea", default);

        Assert.Empty(report.Mistakes);
    }

    [Fact]
    public async Task Counts_CapitalsInEmails()
    {
        _llm.Reply = Review([Fix("dear john", "Dear John,", "punctuation")]);

        var report = await Analyzer().AnalyzeAsync(ReportKind.Email, null, "dear john", default);

        Assert.Single(report.Mistakes);
    }

    [Fact]
    public async Task Marks_MisusedWordsAsWrong()
    {
        _llm.Reply = Review([Fix("recieve", "receive", "spelling")]);

        var report = await Analyzer().AnalyzeAsync(ReportKind.Chat, null, "I recieve mail and I read mail", default);

        Assert.False(report.Words.Single(w => w.Word == "recieve").Correct);
        var mail = report.Words.Single(w => w.Word == "mail");
        Assert.True(mail.Correct);
        Assert.Equal(2, mail.Count);
        Assert.Equal(7, report.TotalWords);
    }

    [Fact]
    public async Task Splits_LongChatsIntoChunks()
    {
        _llm.Reply = Review();
        var source = string.Join('\n', Enumerable.Repeat("This is one line of the learner text.", 3));

        await Analyzer().AnalyzeAsync(ReportKind.Chat, null, source, default);

        Assert.Equal(3, _llm.Requests.Count);
    }

    [Fact]
    public async Task Keeps_AnEmailInOneChunk()
    {
        _llm.Reply = Review();
        var source = string.Join('\n', Enumerable.Repeat("This is one line of the learner text.", 3));

        await Analyzer().AnalyzeAsync(ReportKind.Email, null, source, default);

        Assert.Single(_llm.Requests);
    }

    [Fact]
    public async Task Sends_LearnerTextInUserRoleOnly()
    {
        _llm.Reply = Review();

        await Analyzer().AnalyzeAsync(ReportKind.Chat, null, "secret words here", default);

        var request = Assert.Single(_llm.Requests);
        Assert.DoesNotContain("secret words here", request.System);
        Assert.Contains("secret words here", request.Messages[0].Content);
        Assert.NotNull(request.JsonSchema);
    }

    [Fact]
    public async Task Removes_JsonDebrisFromTextValues()
    {
        _llm.Reply = Review([new { original = "many works", corrected = "a lot of work”,", type = "grammar", explanation = "Work is uncountable.”}]," }]);

        var report = await Analyzer().AnalyzeAsync(ReportKind.Simulation, null, "I have many works", default);

        var mistake = Assert.Single(report.Mistakes);
        Assert.Equal("a lot of work", mistake.Corrected);
        Assert.Equal("Work is uncountable.", mistake.Explanation);
    }

    [Fact]
    public async Task Drops_PhrasingAlreadyCoveredByAMistake()
    {
        _llm.Reply = Review([Fix("many works", "a lot of work")], [new { original = "many works", natural = "a lot of work", why = "Common." }]);

        var report = await Analyzer().AnalyzeAsync(ReportKind.Chat, null, "I have many works", default);

        Assert.Empty(report.Phrasing);
    }

    [Fact]
    public async Task Reads_JsonWrappedInCodeFences()
    {
        _llm.Reply = $"```json\n{Review(rewrite: "Dear team")}\n```";

        var report = await Analyzer().AnalyzeAsync(ReportKind.Email, null, "hello team", default);

        Assert.Equal("Dear team", report.Rewrite);
    }

    [Fact]
    public async Task Retries_ThenFails_OnUnreadableReply()
    {
        _llm.Reply = "not json at all";

        await Assert.ThrowsAsync<PersonaChat.Core.Llm.LlmException>(() =>
            Analyzer().AnalyzeAsync(ReportKind.Chat, null, "some learner text", default));

        Assert.Equal(3, _llm.Requests.Count);
    }
}
