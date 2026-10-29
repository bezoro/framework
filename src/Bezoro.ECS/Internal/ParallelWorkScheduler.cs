using System.Runtime.ExceptionServices;

namespace Bezoro.ECS.Internal;

internal static class ParallelWorkScheduler
{
	[ThreadStatic] private static int _parallelWorkerDepth;

	public static void Execute(
		int itemCount,
		int maxDegreeOfParallelism,
		Action<int> action,
		Func<WaitCallback, bool>? queueWorker = null)
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
		Exception?             captured         = null;
		var                    remainingWorkers = 1;
		var                    completionPublished = 0;
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
				Interlocked.CompareExchange(ref captured, ex, null);
			}
			finally
			{
				_parallelWorkerDepth--;
				if (Interlocked.Decrement(ref remainingWorkers) == 0)
				{
					completion.Set();
					// The waiter also waits for Set to finish before disposing the event.
					Volatile.Write(ref completionPublished, 1);
				}
			}
		};

		for (var workerIndex = 1; workerIndex < workerCount; workerIndex++)
		{
			Interlocked.Increment(ref remainingWorkers);
			bool queued;
			try
			{
				// The optional submission seam must throw only when this callback was not accepted.
				queued = queueWorker is null ? ThreadPool.QueueUserWorkItem(worker) : queueWorker(worker);
			}
			catch (Exception ex)
			{
				Interlocked.Decrement(ref remainingWorkers);
				Interlocked.CompareExchange(ref captured, ex, null);
				break;
			}
			if (!queued)
				worker(null);
		}

		worker(null);
		while (true)
		{
			try
			{
				completion.Wait();
				break;
			}
			catch (ThreadInterruptedException ex)
			{
				// Interruption is reported after all accepted workers have finished.
				Interlocked.CompareExchange(ref captured, ex, null);
			}
		}
		while (Volatile.Read(ref completionPublished) == 0)
			Thread.SpinWait(1);
		if (captured is not null)
			ExceptionDispatchInfo.Capture(captured).Throw();
	}
}
