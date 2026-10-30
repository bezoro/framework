using System.Threading;

namespace Bezoro.BehaviorTrees;

internal static class BehaviorOwnerId
{
    private static long _last;
    internal static long Next()
    {
        while (true)
        {
            long current = Volatile.Read(ref _last);
            if (current == long.MaxValue) throw new InvalidOperationException("Behavior runner identities are exhausted.");
            if (Interlocked.CompareExchange(ref _last, current + 1, current) == current) return current + 1;
        }
    }
}
