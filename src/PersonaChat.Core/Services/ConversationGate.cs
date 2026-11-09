namespace PersonaChat.Core.Services;

/// <summary>Runs one conversation change at a time, so a reset cannot race a chat turn or a note extraction.</summary>
public sealed class ConversationGate
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<T> RunAsync<T>(Func<Task<T>> work, CancellationToken ct)
    {
        await _semaphore.WaitAsync(ct);
        try
        {
            return await work();
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public Task RunAsync(Func<Task> work, CancellationToken ct) =>
        RunAsync(async () =>
        {
            await work();
            return true;
        }, ct);
}
