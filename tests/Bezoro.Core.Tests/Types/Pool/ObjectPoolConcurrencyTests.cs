using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.Core.Types;
using Bezoro.Core.Types.Pool;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types.Pool;

[TestSubject(typeof(ObjectPool<>))]
public class ObjectPoolConcurrencyTests
{
	[Fact]
	public async Task Return_WhenDisposeCompletesDuringReset_ShouldDiscardWithoutPublishing()
	{
		using var resetEntered = new ManualResetEventSlim();
		using var allowReset = new ManualResetEventSlim();
		var discardCount = 0;
		var policy = new PoolPolicy<object>(
			() => new(),
			reset: _ =>
			{
				resetEntered.Set();
				if (!allowReset.Wait(TimeSpan.FromSeconds(10)))
					throw new TimeoutException("Reset was not released after disposal.");

				return true;
			},
			onDiscard: _ => Interlocked.Increment(ref discardCount)
		);
		var pool = new ObjectPool<object>(policy, new() { TrackStatistics = true });
		var item = pool.Rent();
		var returnTask = Task.Run(() => pool.Return(item));

		try
		{
			resetEntered.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
			pool.Dispose();
		}
		finally
		{
			allowReset.Set();
		}

		(await returnTask.WaitAsync(TimeSpan.FromSeconds(10))).Should().BeFalse();
		discardCount.Should().Be(1);
		pool.AvailableCount.Should().Be(0);
		pool.TotalCount.Should().Be(0);
		pool.Statistics.TotalDiscarded.Should().Be(1);
	}

	[Fact]
	public async Task Dispose_WhenReturnsRaceWithDisposal_ShouldNotPublishAfterDisposal()
	{
		const int rounds = 2_000;
		int workerCount = Math.Clamp(Environment.ProcessorCount * 2, 4, 16);
		using var phase = new Barrier(workerCount + 1);
		var failures = new ConcurrentQueue<string>();
		var items = new object[workerCount];
		ObjectPool<object>? pool = null;
		var workers = new Task[workerCount];

		for (var workerIndex = 0; workerIndex < workerCount; workerIndex++)
		{
			int capturedIndex = workerIndex;
			workers[workerIndex] = Task.Factory.StartNew(
				() =>
				{
					for (var round = 0; round < rounds; round++)
					{
						if (!phase.SignalAndWait(TimeSpan.FromSeconds(10)))
							throw new TimeoutException("Return workers did not reach the start phase.");

						try
						{
							pool!.Return(items[capturedIndex]);
						}
						catch (Exception exception)
						{
							failures.Enqueue(exception.ToString());
						}

						if (!phase.SignalAndWait(TimeSpan.FromSeconds(10)))
							throw new TimeoutException("Return workers did not reach the completion phase.");
					}
				},
				CancellationToken.None,
				TaskCreationOptions.LongRunning,
				TaskScheduler.Default
			);
		}

		for (var round = 0; round < rounds; round++)
		{
			pool = new(() => new(), new() { InitialCapacity = workerCount, MaxCapacity = workerCount });
			for (var itemIndex = 0; itemIndex < items.Length; itemIndex++)
				items[itemIndex] = pool.Rent();

			phase.SignalAndWait(TimeSpan.FromSeconds(10)).Should().BeTrue();
			pool.Dispose();
			phase.SignalAndWait(TimeSpan.FromSeconds(10)).Should().BeTrue();

			if (pool.AvailableCount != 0 || pool.TotalCount != 0)
				failures.Enqueue($"Round {round}: Available={pool.AvailableCount}, Total={pool.TotalCount}.");
		}

		await Task.WhenAll(workers).WaitAsync(TimeSpan.FromSeconds(30));
		failures.Should().BeEmpty();
	}
}
