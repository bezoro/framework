using System;
using System.Collections.Concurrent;
using Bezoro.ECS.Internal;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.ECS.Tests.Internal;

[TestSubject(typeof(ParallelWorkScheduler))]
public class ParallelWorkSchedulerTests
{
	[Fact]
	public void Execute_WhenParallelismIsOne_ShouldProcessEveryIndexExactlyOnce()
	{
		var visited = new int[32];

		ParallelWorkScheduler.Execute(32, 1, index => visited[index]++);

		visited.Should().OnlyContain(count => count == 1);
	}

	[Fact]
	public void Execute_WhenParallel_ShouldBoundWorkerThreadCount()
	{
		const int maxDegreeOfParallelism = 3;
		var threadIds = new ConcurrentDictionary<int, byte>();

		ParallelWorkScheduler.Execute(128, maxDegreeOfParallelism, _ =>
		{
			threadIds.TryAdd(Environment.CurrentManagedThreadId, 0);
		});

		threadIds.Count.Should().BeLessThanOrEqualTo(maxDegreeOfParallelism);
	}

	[Fact]
	public void Execute_WhenWorkerThrows_ShouldRethrowOriginalException()
	{
		var act = () => ParallelWorkScheduler.Execute(8, 4, index =>
		{
			if (index == 3)
				throw new InvalidOperationException("boom");
		});

		act.Should().Throw<InvalidOperationException>()
			.WithMessage("boom");
	}
}
