using PersonaChat.Core.Llm;
using PersonaChat.Core.Models;
using PersonaChat.Core.Services;

namespace PersonaChat.Tests;

public sealed class ChatServiceTests : IDisposable
{
    private readonly TestApp _app = new();
    private static CancellationToken Ct => CancellationToken.None;

    public void Dispose() => _app.Dispose();

    [Fact]
    public async Task SendAsync_SavesBothMessages()
    {
        var reply = await _app.Chat().SendAsync(TestApp.PersonaA, " Hi ", ChatMode.Chat, Ct);

        var history = await _app.Chat().GetHistoryAsync(TestApp.PersonaA, Ct);
        Assert.Equal("Hello!", reply);
        Assert.Equal(["Hi", "Hello!"], history.Select(m => m.Content));
    }

    [Fact]
    public async Task SendAsync_KeepsUserTextOutOfSystemPrompt()
    {
        await _app.Chat().SendAsync(TestApp.PersonaA, "secret text", ChatMode.Chat, Ct);

        var request = Assert.Single(_app.Llm.Requests);
        Assert.DoesNotContain("secret text", request.System);
        Assert.Equal(new ChatMessage(ChatRole.User, "secret text"), request.Messages[^1]);
    }

    [Fact]
    public async Task SendAsync_SendsOnlyShortTermHistory()
    {
        for (var i = 0; i < 4; i++)
            await _app.Chat().SendAsync(TestApp.PersonaA, $"m{i}", ChatMode.Chat, Ct);

        Assert.True(_app.Llm.Requests[^1].Messages.Count <= _app.MemoryOptions.ShortTermMessages);
        Assert.Equal(ChatRole.User, _app.Llm.Requests[^1].Messages[0].Role);
    }

    [Fact]
    public async Task SendAsync_SavesNothingWhenModelFails()
    {
        _app.Llm.ShouldFail = true;

        await Assert.ThrowsAsync<LlmException>(() => _app.Chat().SendAsync(TestApp.PersonaA, "Hi", ChatMode.Chat, Ct));

        Assert.Empty(await _app.Chat().GetHistoryAsync(TestApp.PersonaA, Ct));
    }

    [Fact]
    public async Task SendAsync_RejectsUnknownPersona()
    {
        await Assert.ThrowsAsync<PersonaNotFoundException>(() => _app.Chat().SendAsync("nobody", "Hi", ChatMode.Chat, Ct));
    }

    [Fact]
    public async Task History_IsSeparatePerPersona()
    {
        await _app.Chat().SendAsync(TestApp.PersonaA, "only for A", ChatMode.Chat, Ct);
        await _app.Chat().SendAsync(TestApp.PersonaB, "only for B", ChatMode.Chat, Ct);

        var sentToB = _app.Llm.Requests[^1].Messages.Select(m => m.Content);
        Assert.DoesNotContain("only for A", sentToB);
    }

    [Fact]
    public async Task SendAsync_QueuesExtractionWhenDue()
    {
        await _app.Chat().SendAsync(TestApp.PersonaA, "1", ChatMode.Chat, Ct);
        await _app.Chat().SendAsync(TestApp.PersonaA, "2", ChatMode.Chat, Ct);

        await using var queued = _app.Queue.ReadAllAsync(Ct).GetAsyncEnumerator(Ct);
        Assert.True(await queued.MoveNextAsync());
        Assert.Equal(TestApp.PersonaA, queued.Current);
    }
}
