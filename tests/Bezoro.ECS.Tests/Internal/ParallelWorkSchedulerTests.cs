using System;
using System.Collections.Concurrent;
using System.Threading;
using Bezoro.ECS.Internal;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.ECS.Tests.Internal;

[TestSubject(typeof(ParallelWorkScheduler))]
public class ParallelWorkSchedulerTests
{
	[Fact]
	public void Execute_WhenParallel_ShouldBoundWorkerThreadCount()
	{
		const int MAX_DEGREE_OF_PARALLELISM = 3;
		var       threadIds                 = new ConcurrentDictionary<int, byte>();

		ParallelWorkScheduler.Execute(
			128, MAX_DEGREE_OF_PARALLELISM, _ => { threadIds.TryAdd(Environment.CurrentManagedThreadId, 0); }
		);

		threadIds.Count.Should().BeLessThanOrEqualTo(MAX_DEGREE_OF_PARALLELISM);
	}

	[Fact]
	public void Execute_WhenParallelismIsOne_ShouldProcessEveryIndexExactlyOnce()
	{
		var visited = new int[32];

		ParallelWorkScheduler.Execute(32, 1, index => visited[index]++);

		visited.Should().OnlyContain(count => count == 1);
	}

	[Fact]
	public void Execute_WhenWorkerThrows_ShouldRethrowOriginalException()
	{
		var act = () => ParallelWorkScheduler.Execute(
			8, 4, index =>
			{
				if (index == 3)
					throw new InvalidOperationException("boom");
			}
		);

		act.Should().Throw<InvalidOperationException>()
		   .WithMessage("boom");
	}
	[Fact]
	public void NestedExecutions_ReuseEachOuterWorkerWithoutCreatingAnotherWorkerSet()
	{
		var threads = new ConcurrentDictionary<int, byte>();
		int visits = 0;
		ParallelWorkScheduler.Execute(16, 3, _ =>
		{
			int outerThread = Environment.CurrentManagedThreadId;
			ParallelWorkScheduler.Execute(32, 8, _ =>
			{
				Environment.CurrentManagedThreadId.Should().Be(outerThread);
				threads.TryAdd(Environment.CurrentManagedThreadId, 0);
				Interlocked.Increment(ref visits);
			});
		});
		visits.Should().Be(16 * 32);
		threads.Count.Should().BeLessThanOrEqualTo(3);
	}

	[Fact]
	public void NestedFailure_DoesNotLeaveCallerMarkedAsAnActiveWorker()
	{
		Action failing = () => ParallelWorkScheduler.Execute(2, 2, _ =>
			ParallelWorkScheduler.Execute(2, 2, _ => throw new InvalidOperationException("Nested failed.")));
		failing.Should().Throw<InvalidOperationException>();

		using var entered = new CountdownEvent(2);
		ParallelWorkScheduler.Execute(2, 2, _ =>
		{
			entered.Signal();
			entered.Wait(TimeSpan.FromSeconds(10)).Should().BeTrue();
		});
	}

}
