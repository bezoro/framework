using System;
using System.Threading;
using Bezoro.ECS.Options;
using Bezoro.ECS.Services;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.ECS.Tests.Services;

[TestSubject(typeof(World))]
public class WorldTests
{
	[Fact]
	public void AddComponent_Should_BeQueryable()
	{
		// Arrange
		var world  = new World();
		var entity = world.Spawn();
		var input  = new Position { X = 1, Y = 2 };

		// Act
		world.Add(entity, input);
		var output = world.Get<Position>(entity);

		// Assert
		world.Has<Position>(entity).Should().BeTrue();
		output.Should().Be(input);
	}

	[Fact]
	public void AddComponent_WhenCalledDuringQueryIteration_ShouldThrow()
	{
		// Arrange
		var world  = new World();
		var entity = world.Spawn();
		world.Add(entity, new Position { X = 1, Y = 1 });

		// Act
		var act = () =>
		{
			foreach (var _ in world.Query().All<Position>())
				world.Add(entity, new Velocity { X = 0, Y = 0 });
		};

		// Assert
		act.Should().Throw<InvalidOperationException>();
		world.Has<Velocity>(entity).Should().BeFalse();
	}

	[Fact]
	public void Clear_ShouldNotRevalidatePreviousEntityHandle()
	{
		// Arrange
		var world = new World();
		var stale = world.Spawn();

		// Act
		world.Clear();
		var current = world.Spawn();

		// Assert
		stale.Should().NotBe(current);
		world.IsAlive(stale).Should().BeFalse();
		world.IsAlive(current).Should().BeTrue();
	}

	[Fact]
	public void Clear_ShouldResetEntityCount_And_InvalidateExistingEntities()
	{
		// Arrange
		var world  = new World();
		var first  = world.Spawn();
		var second = world.Spawn();

		// Act
		world.Clear();

		// Assert
		world.EntityCount.Should().Be(0);
		world.IsAlive(first).Should().BeFalse();
		world.IsAlive(second).Should().BeFalse();
	}

	[Fact]
	public void ComponentTypeIds_WhenDifferentWorldsRegisterDifferentTypes_ShouldRemainWorldScoped()
	{
		var worldA            = new World();
		int worldAFirstCustom = worldA.GetOrCreateComponentTypeId<WorldAOnlyComponent>();

		var worldB = new World();
		worldB.GetOrCreateComponentTypeId<WorldBOnlyComponent>();
		worldB.GetOrCreateComponentTypeId<WorldBSecondOnlyComponent>();

		int worldASecondCustom = worldA.GetOrCreateComponentTypeId<WorldBSecondOnlyComponent>();

		worldASecondCustom.Should().Be(worldAFirstCustom + 1);
	}

	[Fact]
	public void CreateEntity_WhenIdIsRecycled_ShouldBumpEntityVersion()
	{
		var world    = new World();
		var original = world.Spawn();
		world.Despawn(original);

		var recycled = world.Spawn();

		recycled.Id.Should().Be(original.Id);
		recycled.Version.Should().NotBe(original.Version);
	}

	[Fact]
	public void GetOrCreateArchetype_ShouldReturnSameInstance_WhenSameComponentSet()
	{
		// Arrange
		var world = new World();

		// Act
		var first  = world.GetOrCreateArchetype(typeof(Position), typeof(Velocity));
		var second = world.GetOrCreateArchetype(typeof(Velocity), typeof(Position));

		// Assert
		first.Should().BeSameAs(second);
	}

	[Fact]
	public void IsAlive_WhenEntityBelongsToDifferentWorld_ShouldBeFalse()
	{
		// Arrange
		var worldA  = new World();
		var worldB  = new World();
		var foreign = worldA.Spawn();
		worldB.Spawn();

		// Act
		bool isAlive = worldB.IsAlive(foreign);

		// Assert
		isAlive.Should().BeFalse();
	}

	[Fact]
	public void Query_ForEachParallel_WhenActionThrows_ShouldRethrowOriginalException()
	{
		var world = new World(
			new WorldOptions
			{
				ChunkCapacity          = 1,
				MaxDegreeOfParallelism = 4
			}
		);

		for (var i = 0; i < 16; i++)
		{
			var entity = world.Spawn();
			world.Add(entity, new Position { X = i, Y = i });
		}

		var act = () =>
			world.Query().All<Position>().ForEachParallel(_ => throw new InvalidOperationException("query-fail"));

		act.Should().Throw<InvalidOperationException>()
		   .WithMessage("query-fail");
	}

	[Fact]
	public void Query_ForEachParallel_WhenManyChunks_ShouldProcessEveryEntityExactlyOnce()
	{
		// Arrange
		var world = new World(
			new WorldOptions
			{
				ChunkCapacity          = 1,
				MaxDegreeOfParallelism = 4
			}
		);

		const int ENTITY_COUNT = 128;
		var       expectedSum  = 0;
		for (var i = 1; i <= ENTITY_COUNT; i++)
		{
			var entity = world.Spawn();
			world.Add(entity, new Position { X = i, Y = 0 });
			expectedSum += i;
		}

		var  processedCount = 0;
		long processedSum   = 0;

		// Act
		world.Query().All<Position>().ForEachParallel(
			chunk =>
			{
				var positions = chunk.ReadOnlyComponents<Position>();
				for (var i = 0; i < chunk.Count; i++)
				{
					Interlocked.Increment(ref processedCount);
					Interlocked.Add(ref processedSum, (long)positions[i].X);
				}
			},
			4
		);

		// Assert
		processedCount.Should().Be(ENTITY_COUNT);
		processedSum.Should().Be(expectedSum);
	}

	[Fact]
	public void Query_ShouldReturnAllEntities_WithPositionAndVelocity()
	{
		// Arrange
		var world = new World();
		var e1    = world.Spawn();
		world.Add(e1, new Position { X = 1, Y    = 1 });
		world.Add(e1, new Velocity { X = 0.5f, Y = 0.25f });

		var e2 = world.Spawn();
		world.Add(e2, new Position { X = 2, Y    = 2 });
		world.Add(e2, new Velocity { X = 1.5f, Y = -0.5f });

		var e3 = world.Spawn();
		world.Add(e3, new Position { X = 3, Y = 3 });

		// Act
		var count = 0;
		foreach (var chunk in world.Query().All<Position>().All<Velocity>())
			count += chunk.Count;

		// Assert
		count.Should().Be(2);
	}

	[Fact]
	public void Query_WithAnyTypeArrayFilter_ShouldMatchEntitiesWithAtLeastOneRequestedComponent()
	{
		var world = new World();

		var positionOnly = world.Spawn();
		world.Add(positionOnly, new Position { X = 1, Y = 1 });

		var velocityOnly = world.Spawn();
		world.Add(velocityOnly, new Velocity { X = 1, Y = 1 });

		var both = world.Spawn();
		world.Add(both, new Position { X = 2, Y = 2 });
		world.Add(both, new Velocity { X = 2, Y = 2 });

		world.Spawn();

		var count = 0;
		foreach (var chunk in world.Query().Any(typeof(Position), typeof(Velocity)))
			count += chunk.Count;

		count.Should().Be(3);
	}

	[Fact]
	public void Query_WithArchetypeFilter_ShouldReturnOnlyExactArchetype()
	{
		// Arrange
		var world             = new World();
		var baseArchetype     = world.GetOrCreateArchetype(typeof(Position), typeof(Velocity));
		var extendedArchetype = world.GetOrCreateArchetype(typeof(Position), typeof(Velocity), typeof(Health));

		world.Spawn(new Position { X = 1, Y = 1 }, new Velocity { X = 2, Y = 2 });
		world.Spawn(new Position { X = 3, Y = 3 }, new Velocity { X = 4, Y = 4 }, new Health());

		// Act
		var allCount = 0;
		foreach (var chunk in world.Query().All<Position>().All<Velocity>())
			allCount += chunk.Count;

		var filteredCount = 0;
		foreach (var chunk in world.Query(baseArchetype).All<Position>().All<Velocity>())
			filteredCount += chunk.Count;

		// Assert
		allCount.Should().Be(2);
		filteredCount.Should().Be(1);
	}

	[Fact]
	public void Query_WithWithoutFilter_ShouldExcludeEntitiesWithExcludedComponents()
	{
		// Arrange
		var world = new World();

		var posOnly = world.Spawn();
		world.Add(posOnly, new Position { X = 1, Y = 1 });

		var posVelocity = world.Spawn();
		world.Add(posVelocity, new Position { X = 2, Y = 2 });
		world.Add(posVelocity, new Velocity { X = 1, Y = 1 });

		var posHealth = world.Spawn();
		world.Add(posHealth, new Position { X = 3, Y = 3 });
		world.Add(posHealth, new Health());

		var posVelocityHealth = world.Spawn();
		world.Add(posVelocityHealth, new Position { X = 4, Y = 4 });
		world.Add(posVelocityHealth, new Velocity { X = 2, Y = 2 });
		world.Add(posVelocityHealth, new Health());

		// Act
		var count = 0;
		foreach (var chunk in world.Query().All<Position>().None(typeof(Health), typeof(Velocity), typeof(Health)))
			count += chunk.Count;

		// Assert
		count.Should().Be(1);
	}

	[Fact]
	public void Query_WithWithoutFilter_ShouldWorkInParallelPath()
	{
		// Arrange
		var world = new World(
			new WorldOptions
			{
				ChunkCapacity          = 1,
				MaxDegreeOfParallelism = 4
			}
		);

		const int ENTITY_COUNT = 20;
		for (var i = 0; i < ENTITY_COUNT; i++)
		{
			var entity = world.Spawn();
			world.Add(entity, new Position { X = i, Y = i });

			if (i % 2 == 0)
				world.Add(entity, new Velocity { X = 1, Y = 1 });
		}

		// Act
		var count = 0;
		world.Query().All<Position>().None<Velocity>().ForEachParallel(
			chunk => Interlocked.Add(ref count, chunk.Count), 4
		);

		// Assert
		count.Should().Be(ENTITY_COUNT / 2);
	}

	[Fact]
	public void QueryCache_WhenOnlyChangedFilterDiffers_ShouldReuseArchetypeMatchCache()
	{
		var world  = new World();
		var entity = world.Spawn();
		world.Add(entity, new Position { X = 1, Y = 1 });

		foreach (var _ in world.Query().All<Position>()) { }

		world.QueryCacheEntryCount.Should().Be(1);

		foreach (var _ in world.Query().All<Position>().Changed<Position>()) { }

		world.QueryCacheEntryCount.Should().Be(1);
	}

	[Fact]
	public void SetComponent_WhenEntityBelongsToDifferentWorld_ShouldThrow()
	{
		// Arrange
		var worldA  = new World();
		var worldB  = new World();
		var foreign = worldA.Spawn();
		var local   = worldB.Spawn();

		// Act
		var act = () => worldB.Set(foreign, new Position { X = 5, Y = 6 });

		// Assert
		act.Should().Throw<InvalidOperationException>();
		worldB.Has<Position>(local).Should().BeFalse();
	}

	private struct Health;

	private struct Position	{
		public float X;
		public float Y;
	}

	private struct Velocity	{
		public float X;
		public float Y;
	}

	private struct WorldAOnlyComponent;

	private struct WorldBOnlyComponent;

	private struct WorldBSecondOnlyComponent;
}
