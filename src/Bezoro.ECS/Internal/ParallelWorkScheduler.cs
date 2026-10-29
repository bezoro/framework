using System.Runtime.ExceptionServices;

namespace Bezoro.ECS.Internal;

internal static class ParallelWorkScheduler
{
	[ThreadStatic] private static int _parallelWorkerDepth;

	public static void Execute(int itemCount, int maxDegreeOfParallelism, Action<int> action)
	{
		if (itemCount < 0)
			throw new ArgumentOutOfRangeException(nameof(itemCount), "Item count must be non-negative.");

		if (maxDegreeOfParallelism <= 0)
			throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism), "Parallelism must be positive.");

		if (action is null)
			throw new ArgumentNullException(nameof(action));

		if (itemCount == 0)
			return;

		// Reuse an active worker for nested system/query/world scheduling rather than
		// multiplying worker sets and blocking a thread pool full of outer workers.
		int workerCount = _parallelWorkerDepth > 0 ? 1 : Math.Min(itemCount, maxDegreeOfParallelism);
		if (workerCount == 1)
		{
			for (var i = 0; i < itemCount; i++)
				action(i);

			return;
		}

		int                    nextIndex        = -1;
		ExceptionDispatchInfo? captured         = null;
		var                    remainingWorkers = 1;
		using var              completion       = new ManualResetEventSlim(false);
		WaitCallback worker = _ =>
		{
			_parallelWorkerDepth++;
			try
			{
				while (captured is null)
				{
					int index = Interlocked.Increment(ref nextIndex);
					if (index >= itemCount)
						break;

					action(index);
				}
			}
			catch (Exception ex)
			{
				Interlocked.CompareExchange(ref captured, ExceptionDispatchInfo.Capture(ex), null);
			}
			finally
			{
				_parallelWorkerDepth--;
				if (Interlocked.Decrement(ref remainingWorkers) == 0)
					completion.Set();
			}
		};

		for (var workerIndex = 1; workerIndex < workerCount; workerIndex++)
		{
			Interlocked.Increment(ref remainingWorkers);
			if (!ThreadPool.QueueUserWorkItem(worker))
				worker(null);
		}

		worker(null);
		completion.Wait();
		captured?.Throw();
	}
}
