using System.Threading;

namespace Bezoro.Core.Collections;

internal static class CollectionOwnerId
{
	private static long _last;

	internal static long Next()
	{
		while (true)
		{
			long previous = Volatile.Read(ref _last);
			if (previous == long.MaxValue)
				throw new InvalidOperationException("Collection owner identifiers are exhausted.");
			long next = previous + 1;
			if (Interlocked.CompareExchange(ref _last, next, previous) == previous) return next;
		}
	}
}
