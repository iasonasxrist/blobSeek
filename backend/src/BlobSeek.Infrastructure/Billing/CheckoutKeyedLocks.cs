using System.Collections.Concurrent;

namespace BlobSeek.Infrastructure.Billing;

internal static class CheckoutKeyedLocks
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new(StringComparer.Ordinal);

    public static async ValueTask<IAsyncDisposable> AcquireAsync(string key, CancellationToken cancellationToken)
    {
        var gate = Locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        return new Releaser(key, gate);
    }

    private sealed class Releaser(string key, SemaphoreSlim gate) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            gate.Release();
            if (gate.CurrentCount == 1)
            {
                Locks.TryRemove(new KeyValuePair<string, SemaphoreSlim>(key, gate));
            }

            return ValueTask.CompletedTask;
        }
    }
}
