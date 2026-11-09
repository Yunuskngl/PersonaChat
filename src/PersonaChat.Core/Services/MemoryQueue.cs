using System.Threading.Channels;

namespace PersonaChat.Core.Services;

/// <summary>Work that runs in the background, so replies are not delayed by it.</summary>
public abstract class BackgroundQueue<T>
{
    private readonly Channel<T> _channel = Channel.CreateUnbounded<T>();

    public void Enqueue(T item) => _channel.Writer.TryWrite(item);

    public IAsyncEnumerable<T> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}

/// <summary>Persona ids waiting for note extraction.</summary>
public sealed class MemoryQueue : BackgroundQueue<string>;

/// <summary>Report ids waiting to be generated.</summary>
public sealed class ReportQueue : BackgroundQueue<int>;
