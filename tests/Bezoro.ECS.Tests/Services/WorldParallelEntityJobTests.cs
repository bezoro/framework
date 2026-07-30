using System;
using System.Linq;
using Bezoro.ECS.Internal.Fixed;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.ECS.Tests.Services;

[TestSubject(typeof(World))]
public class WorldParallelEntityJobTests
{
	[Fact]
	public void QueryView_RunParallelEntity_WhenEntitySlotIsRecycled_ShouldUseCurrentVersion()
	{
		using var world = new World(new WorldConfig { ChunkCapacity = 1, MaxDegreeOfParallelism = 4 });
		_ = world.Spawn(
			new ParallelEntityProbeComponent { Index = 0 },
			new ParallelEntityProbeInput2(),
			new ParallelEntityProbeInput3(),
			new ParallelEntityProbeInput4()
		);
		var recycled = world.Spawn(
			new ParallelEntityProbeComponent { Index = 1 },
			new ParallelEntityProbeInput2(),
			new ParallelEntityProbeInput3(),
			new ParallelEntityProbeInput4()
		);
		world.Despawn(recycled);
		var replacement = world.Spawn(
			new ParallelEntityProbeComponent { Index = 1 },
			new ParallelEntityProbeInput2(),
			new ParallelEntityProbeInput3(),
			new ParallelEntityProbeInput4()
		);
		var receivedEntities = new Entity[2];
		var checksums         = new int[2];
		var visitCounts       = new int[2];
		var query             = world.Query<ParallelEntityProbeQuery>();

		query.RunParallelEntity<ParallelEntityProbeJob, ParallelEntityProbeComponent>(
			new(receivedEntities, checksums, visitCounts),
			4
		);

		replacement.Id.Should().Be(recycled.Id);
		replacement.Version.Should().BeGreaterThan(recycled.Version);
		receivedEntities[1].Should().Be(replacement);
		receivedEntities.Should().NotContain(recycled);
		visitCounts.Should().Equal(1, 1);
	}

	[Fact]
	public void QueryView_RunParallelEntity_WhenCursorOwnsSharedScratch_ShouldPreserveLazyCursorResults()
	{
		using var world = new World(new WorldConfig { ChunkCapacity = 16, MaxDegreeOfParallelism = 4 });
		var entities = Enumerable.Range(0, 12)
			.Select(index => world.Spawn(
				new ParallelEntityProbeComponent { Index = index },
				new ParallelEntityProbeInput2 { Value = index + 10 },
				new ParallelEntityProbeInput3 { Value = index + 20 },
				new ParallelEntityProbeInput4 { Value = index + 30 }
			))
			.ToArray();
		var allHandle     = world.Compile<ParallelEntityProbeQuery>();
		var changedHandle = world.Compile<ChangedParallelEntityProbeQuery>();
		using (var initial = world.Execute(changedHandle))
		{
			initial.MoveNext().Should().BeTrue();
			initial.Current.ToArray().Should().Equal(entities);
		}

		int[] changedIndices = [1, 5, 9];
		foreach (int index in changedIndices)
			world.Write<ParallelEntityProbeComponent>(entities[index]).ExecutionCount = 0;

		using var cursor = world.Execute(allHandle);
		cursor.MoveNext().Should().BeTrue();
		var receivedEntities = new Entity[entities.Length];
		var checksums         = new int[entities.Length];
		var visitCounts       = new int[entities.Length];
		var query             = new QueryView<ChangedParallelEntityProbeQuery>(world, changedHandle);

		query.RunParallelEntity<ParallelEntityProbeJob, ParallelEntityProbeComponent>(
			new(receivedEntities, checksums, visitCounts),
			4
		);

		foreach (int index in changedIndices)
		{
			receivedEntities[index].Should().Be(entities[index]);
			visitCounts[index].Should().Be(1);
		}
		visitCounts.Where((_, index) => !changedIndices.Contains(index)).Should().OnlyContain(count => count == 0);
		cursor.Current.ToArray().Should().Equal(entities);
	}

	[Fact]
	public void QueryView_RunParallelEntity3_WhenQuerySpansChunks_ShouldVisitEveryEntityExactlyOnce()
	{
		using var world = new World(new WorldConfig { ChunkCapacity = 2, MaxDegreeOfParallelism = 4 });
		var entities = Enumerable.Range(0, 13)
			.Select(index => world.Spawn(
				new ParallelEntityProbeComponent { Index = index },
				new ParallelEntityProbeInput2 { Value = index + 10 },
				new ParallelEntityProbeInput3 { Value = index * 3 },
				new ParallelEntityProbeInput4 { Value = index + 30 }
			))
			.ToArray();
		var receivedEntities = new Entity[entities.Length];
		var checksums         = new int[entities.Length];
		var visitCounts       = new int[entities.Length];

		world.Query<ParallelEntityProbeQuery>()
			.RunParallelEntity<ParallelEntityProbeJob, ParallelEntityProbeComponent, ParallelEntityProbeInput2,
				ParallelEntityProbeInput3>(new(receivedEntities, checksums, visitCounts), 4);

		receivedEntities.Should().Equal(entities);
		visitCounts.Should().OnlyContain(count => count == 1);
		checksums.Should().Equal(Enumerable.Range(0, entities.Length).Select(index => index + 10 + index * 3));
		for (var index = 0; index < entities.Length; index++)
			world.Read<ParallelEntityProbeComponent>(entities[index]).ExecutionCount.Should().Be(1);
	}

	[Fact]
	public void QueryView_RunParallelEntity4_WhenQuerySpansChunks_ShouldVisitEveryEntityExactlyOnce()
	{
		using var world = new World(new WorldConfig { ChunkCapacity = 2, MaxDegreeOfParallelism = 4 });
		var entities = Enumerable.Range(0, 13)
			.Select(index => world.Spawn(
				new ParallelEntityProbeComponent { Index = index },
				new ParallelEntityProbeInput2 { Value = index + 10 },
				new ParallelEntityProbeInput3 { Value = index * 3 },
				new ParallelEntityProbeInput4 { Value = 100 - index }
			))
			.ToArray();
		var receivedEntities = new Entity[entities.Length];
		var checksums         = new int[entities.Length];
		var visitCounts       = new int[entities.Length];

		world.Query<ParallelEntityProbeQuery>()
			.RunParallelEntity<ParallelEntityProbeJob, ParallelEntityProbeComponent, ParallelEntityProbeInput2,
				ParallelEntityProbeInput3, ParallelEntityProbeInput4>(
				new(receivedEntities, checksums, visitCounts),
				4
			);

		receivedEntities.Should().Equal(entities);
		visitCounts.Should().OnlyContain(count => count == 1);
		checksums.Should().Equal(Enumerable.Range(0, entities.Length).Select(index => 110 + index * 3));
		for (var index = 0; index < entities.Length; index++)
			world.Read<ParallelEntityProbeComponent>(entities[index]).ExecutionCount.Should().Be(1);
	}

	[Fact]
	public void QueryView_RunParallelEntity_WhenWorldIsDisposedAndHandleAndDegreeAreInvalid_ShouldPreferDisposedValidation()
	{
		using var handleOwner = new World();
		var handle = handleOwner.Compile<ParallelEntityProbeQuery>();
		var world  = new World();
		var query  = new QueryView<ParallelEntityProbeQuery>(world, handle);
		world.Dispose();

		var action = () => query.RunParallelEntity<ParallelEntityProbeJob, ParallelEntityProbeComponent>(
			new([], [], []),
			0
		);

		action.Should().Throw<ObjectDisposedException>();
	}

	[Fact]
	public void QueryView_RunParallelEntity_WhenHandleBelongsToDifferentWorldAndDegreeIsInvalid_ShouldPreferHandleValidation()
	{
		using var handleOwner = new World();
		using var world       = new World();
		var handle = handleOwner.Compile<ParallelEntityProbeQuery>();
		var query  = new QueryView<ParallelEntityProbeQuery>(world, handle);

		var action = () => query.RunParallelEntity<ParallelEntityProbeJob, ParallelEntityProbeComponent>(
			new([], [], []),
			0
		);

		action.Should().Throw<InvalidOperationException>()
			.WithMessage("*different world*");
	}

	[Fact]
	public void QueryView_RunParallelEntity_WhenDegreeOfParallelismIsInvalid_ShouldNameParameter()
	{
		using var world = new World();
		var query = world.Query<ParallelEntityProbeQuery>();

		var action = () => query.RunParallelEntity<ParallelEntityProbeJob, ParallelEntityProbeComponent>(
			new([], [], []),
			0
		);

		action.Should().Throw<ArgumentOutOfRangeException>()
			.WithParameterName("degreeOfParallelism");
	}

	[Fact]
	public void QueryView_RunParallelEntity_WhenWorkerThrows_ShouldPropagateExactExceptionAndReleaseScratch()
	{
		using var world = new World(new WorldConfig { ChunkCapacity = 1, MaxDegreeOfParallelism = 4 });
		var entities = Enumerable.Range(0, 8)
			.Select(index => world.Spawn(
				new ParallelEntityProbeComponent { Index = index },
				new ParallelEntityProbeInput2(),
				new ParallelEntityProbeInput3(),
				new ParallelEntityProbeInput4()
			))
			.ToArray();
		var query    = world.Query<ParallelEntityProbeQuery>();
		var expected = new InvalidOperationException("Expected worker failure.");
		Exception? caught = null;

		try
		{
			query.RunParallelEntity<ThrowingParallelEntityProbeJob, ParallelEntityProbeComponent>(new(expected), 4);
		}
		catch (Exception exception)
		{
			caught = exception;
		}

		caught.Should().BeSameAs(expected);
		world.AcquireQueryChunkMatchScratchForDirectIteration(
			out QueryChunkMatch[] chunkMatches,
			out bool usesSharedScratch
		);
		try
		{
			usesSharedScratch.Should().BeTrue();
		}
		finally
		{
			world.ReleaseQueryChunkMatchScratchForDirectIteration(chunkMatches, usesSharedScratch);
		}

		var receivedEntities = new Entity[entities.Length];
		var checksums         = new int[entities.Length];
		var visitCounts       = new int[entities.Length];
		query.RunParallelEntity<ParallelEntityProbeJob, ParallelEntityProbeComponent>(
			new(receivedEntities, checksums, visitCounts),
			4
		);
		receivedEntities.Should().Equal(entities);
		visitCounts.Should().OnlyContain(count => count == 1);
	}

	[Fact]
	public void QueryView_RunParallelEntity_WhenMutating_ShouldTrackExactlyMatchingEntitiesAsChanged()
	{
		using var world = new World(new WorldConfig { ChunkCapacity = 1, MaxDegreeOfParallelism = 4 });
		var matching = Enumerable.Range(0, 6)
			.Select(index => world.Spawn(
				new ParallelEntityProbeComponent { Index = index },
				new ParallelEntityProbeInput2 { Value = index },
				new ParallelEntityProbeInput3 { Value = index },
				new ParallelEntityProbeInput4 { Value = index }
			))
			.ToArray();
		Entity[] nonmatching =
		[
			world.Spawn(new ParallelEntityProbeComponent { Index = 6 }),
			world.Spawn(new ParallelEntityProbeComponent { Index = 7 })
		];
		var changedHandle = world.Compile<ChangedParallelEntityProbeQuery>();
		using (var initial = world.Execute(changedHandle))
		{
			initial.MoveNext().Should().BeTrue();
			initial.Current.Length.Should().Be(8);
		}
		var receivedEntities = new Entity[matching.Length];
		var checksums         = new int[matching.Length];
		var visitCounts       = new int[matching.Length];

		world.Query<ParallelEntityProbeQuery>()
			.RunParallelEntity<ParallelEntityProbeJob, ParallelEntityProbeComponent, ParallelEntityProbeInput2,
				ParallelEntityProbeInput3, ParallelEntityProbeInput4>(
				new(receivedEntities, checksums, visitCounts),
				4
			);

		receivedEntities.Should().Equal(matching);
		visitCounts.Should().OnlyContain(count => count == 1);
		foreach (Entity entity in matching)
			world.Read<ParallelEntityProbeComponent>(entity).ExecutionCount.Should().Be(1);
		foreach (Entity entity in nonmatching)
			world.Read<ParallelEntityProbeComponent>(entity).ExecutionCount.Should().Be(0);

		using var changed = world.Execute(changedHandle);
		changed.MoveNext().Should().BeTrue();
		changed.Current.ToArray().Should().BeEquivalentTo(matching);
	}
}
