using System.Collections.Concurrent;

namespace DataForge.Web;

/// <summary>Enqueues reports synchronously so completion cannot overtake pending callbacks.</summary>
public sealed class QueuedProgress<T> : IProgress<T>
{
    private readonly ConcurrentQueue<T> queue = new();
    public void Report(T value) => queue.Enqueue(value);
    public bool TryDequeue(out T value) => queue.TryDequeue(out value!);
}
