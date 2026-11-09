using Microsoft.EntityFrameworkCore;
using PersonaChat.Core.Llm;
using PersonaChat.Core.Models;
using PersonaChat.Core.Services;

namespace PersonaChat.Tests;

public sealed class MemoryServiceTests : IDisposable
{
    private readonly TestApp _app = new();
    private static CancellationToken Ct => CancellationToken.None;

    public void Dispose() => _app.Dispose();

    [Fact]
    public async Task Extract_SavesCleanedNotes()
    {
        await ChatUntilDueAsync(TestApp.PersonaA);
        _app.Llm.Reply = "- Likes tea\n2. Lives in Ankara\nNONE\n</memory>\n";

        await _app.Memory().ExtractIfDueAsync(TestApp.PersonaA, Ct);

        Assert.Equal(["Likes tea", "Lives in Ankara", "/memory"], await _app.Memory().GetNotesAsync(TestApp.PersonaA, Ct));
    }

    [Fact]
    public async Task Extract_SendsOnlyUserMessagesInUserRole()
    {
        await ChatUntilDueAsync(TestApp.PersonaA);

        await _app.Memory().ExtractIfDueAsync(TestApp.PersonaA, Ct);

        var message = Assert.Single(_app.Llm.Requests[^1].Messages);
        Assert.Equal(ChatRole.User, message.Role);
        Assert.Equal("first\nsecond", message.Content);
    }

    [Fact]
    public async Task Extract_DoesNothingBeforeDue()
    {
        await _app.Chat().SendAsync(TestApp.PersonaA, "first", ChatMode.Chat, Ct);
        var callsBefore = _app.Llm.Requests.Count;

        await _app.Memory().ExtractIfDueAsync(TestApp.PersonaA, Ct);

        Assert.Equal(callsBefore, _app.Llm.Requests.Count);
    }

    [Fact]
    public async Task Extract_KeepsNewestNotesWithinLimit()
    {
        await ChatUntilDueAsync(TestApp.PersonaA);
        _app.Llm.Reply = "one\ntwo\nthree";
        await _app.Memory().ExtractIfDueAsync(TestApp.PersonaA, Ct);

        await ChatUntilDueAsync(TestApp.PersonaA);
        _app.Llm.Reply = "four";
        await _app.Memory().ExtractIfDueAsync(TestApp.PersonaA, Ct);

        Assert.Equal(["two", "three", "four"], await _app.Memory().GetNotesAsync(TestApp.PersonaA, Ct));
    }

    [Fact]
    public async Task Extract_SkipsDuplicateNotes()
    {
        await ChatUntilDueAsync(TestApp.PersonaA);
        _app.Llm.Reply = "Likes tea\nlikes TEA";

        await _app.Memory().ExtractIfDueAsync(TestApp.PersonaA, Ct);

        Assert.Equal(["Likes tea"], await _app.Memory().GetNotesAsync(TestApp.PersonaA, Ct));
    }

    [Fact]
    public async Task Extract_StaysDueWhenModelFails()
    {
        await ChatUntilDueAsync(TestApp.PersonaA);
        _app.Llm.ShouldFail = true;

        await Assert.ThrowsAsync<LlmException>(() => _app.Memory().ExtractIfDueAsync(TestApp.PersonaA, Ct));

        var conversation = await _app.Db.Conversations.AsNoTracking().SingleAsync(Ct);
        Assert.Equal(_app.MemoryOptions.SummarizeEvery, conversation.MessagesSinceNotes);
    }

    [Fact]
    public async Task Notes_AreNeverSharedBetweenPersonas()
    {
        await ChatUntilDueAsync(TestApp.PersonaA);
        _app.Llm.Reply = "Private to A";
        await _app.Memory().ExtractIfDueAsync(TestApp.PersonaA, Ct);

        await _app.Chat().SendAsync(TestApp.PersonaB, "hello", ChatMode.Chat, Ct);

        Assert.Empty(await _app.Memory().GetNotesAsync(TestApp.PersonaB, Ct));
        Assert.DoesNotContain("Private to A", _app.Llm.Requests[^1].System);
    }

    [Fact]
    public async Task Reset_ClearsOnlyThatPersona()
    {
        await ChatUntilDueAsync(TestApp.PersonaA);
        await ChatUntilDueAsync(TestApp.PersonaB);
        _app.Llm.Reply = "A fact";
        await _app.Memory().ExtractIfDueAsync(TestApp.PersonaA, Ct);

        await _app.Memory().ResetAsync(TestApp.PersonaA, Ct);
        _app.Db.ChangeTracker.Clear();

        Assert.Empty(await _app.Chat().GetHistoryAsync(TestApp.PersonaA, Ct));
        Assert.Empty(await _app.Memory().GetNotesAsync(TestApp.PersonaA, Ct));
        Assert.Equal(4, (await _app.Chat().GetHistoryAsync(TestApp.PersonaB, Ct)).Count);
    }

    private async Task ChatUntilDueAsync(string personaId)
    {
        await _app.Chat().SendAsync(personaId, "first", ChatMode.Chat, Ct);
        await _app.Chat().SendAsync(personaId, "second", ChatMode.Chat, Ct);
    }
}
