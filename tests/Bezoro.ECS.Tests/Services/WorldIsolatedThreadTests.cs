using System;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;
using FluentAssertions;
using Xunit;

namespace Bezoro.ECS.Tests.Services;

public sealed class WorldIsolatedThreadTests
{
	[Fact]
	public async Task IndependentWorldsOnDifferentThreads_DoNotShareComponentTypeCacheEntries()
	{
		using var start = new Barrier(4);
		var tasks = new Task[4];
		for (int worker = 0; worker < tasks.Length; worker++)
		{
			int workerId = worker;
			tasks[worker] = Task.Factory.StartNew(() =>
			{
				using var world = new World(new WorldConfig
				{
					EntityCapacity = 2,
					ComponentTypeCapacity = 4,
					CommandCapacity = 4,
					CommandPayloadCapacityPerType = 4,
					QueryResultCapacity = 2,
					ChunkCapacity = 2
				});
				Entity entity = world.Spawn();
				// Deliberately register the shared component at different type IDs.
				if (workerId % 2 == 0) world.Set(entity, new OtherComponent { Value = -1 });
				world.Set(entity, new SharedComponent { Value = workerId });
				start.SignalAndWait(TimeSpan.FromSeconds(10)).Should().BeTrue();
				for (int iteration = 0; iteration < 20_000; iteration++)
				{
					world.Set(entity, new SharedComponent { Value = iteration + workerId });
					if (world.Read<SharedComponent>(entity).Value != iteration + workerId)
						throw new InvalidOperationException("A different world corrupted the component type cache.");
				}
			}, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
		}

		await Task.WhenAll(tasks);
	}

	private struct SharedComponent { public int Value; }
	private struct OtherComponent { public int Value; }
}
