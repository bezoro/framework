using System;
using System.Threading;
using Bezoro.ECS.Abstractions;
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
		var entity = world.CreateEntity();
		var input  = new Position { X = 1, Y = 2 };

		// Act
		world.AddComponent(entity, input);
		var output = world.GetComponent<Position>(entity);

		// Assert
		world.HasComponent<Position>(entity).Should().BeTrue();
		output.Should().Be(input);
	}

	[Fact]
	public void Clear_ShouldNotRevalidatePreviousEntityHandle()
	{
		// Arrange
		var world = new World();
		var stale = world.CreateEntity();

		// Act
		world.Clear();
		var current = world.CreateEntity();

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
		var first  = world.CreateEntity();
		var second = world.CreateEntity();

		// Act
		world.Clear();

		// Assert
		world.EntityCount.Should().Be(0);
		world.IsAlive(first).Should().BeFalse();
		world.IsAlive(second).Should().BeFalse();
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
		var foreign = worldA.CreateEntity();
		worldB.CreateEntity();

		// Act
		bool isAlive = worldB.IsAlive(foreign);

		// Assert
		isAlive.Should().BeFalse();
	}

	[Fact]
	public void Query_ShouldReturnAllEntities_WithPositionAndVelocity()
	{
		// Arrange
		var world = new World();
		var e1    = world.CreateEntity();
		world.AddComponent(e1, new Position { X = 1, Y    = 1 });
		world.AddComponent(e1, new Velocity { X = 0.5f, Y = 0.25f });

		var e2 = world.CreateEntity();
		world.AddComponent(e2, new Position { X = 2, Y    = 2 });
		world.AddComponent(e2, new Velocity { X = 1.5f, Y = -0.5f });

		var e3 = world.CreateEntity();
		world.AddComponent(e3, new Position { X = 3, Y = 3 });

		// Act
		var count = 0;
		foreach (var chunk in world.Query().With<Position>().With<Velocity>())
			count += chunk.Count;

		// Assert
		count.Should().Be(2);
	}

	[Fact]
	public void Query_WithArchetypeFilter_ShouldReturnOnlyExactArchetype()
	{
		// Arrange
		var world             = new World();
		var baseArchetype     = world.GetOrCreateArchetype(typeof(Position), typeof(Velocity));
		var extendedArchetype = world.GetOrCreateArchetype(typeof(Position), typeof(Velocity), typeof(Health));

		world.CreateEntity(baseArchetype);
		world.CreateEntity(extendedArchetype);

		// Act
		var allCount = 0;
		foreach (var chunk in world.Query().With<Position>().With<Velocity>())
			allCount += chunk.Count;

		var filteredCount = 0;
		foreach (var chunk in world.Query(baseArchetype).With<Position>().With<Velocity>())
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

		var posOnly = world.CreateEntity();
		world.AddComponent(posOnly, new Position { X = 1, Y = 1 });

		var posVelocity = world.CreateEntity();
		world.AddComponent(posVelocity, new Position { X = 2, Y = 2 });
		world.AddComponent(posVelocity, new Velocity { X = 1, Y = 1 });

		var posHealth = world.CreateEntity();
		world.AddComponent(posHealth, new Position { X = 3, Y = 3 });
		world.AddComponent(posHealth, new Health());

		var posVelocityHealth = world.CreateEntity();
		world.AddComponent(posVelocityHealth, new Position { X = 4, Y = 4 });
		world.AddComponent(posVelocityHealth, new Velocity { X = 2, Y = 2 });
		world.AddComponent(posVelocityHealth, new Health());

		// Act
		var count = 0;
		foreach (var chunk in world.Query().With<Position>().Without(typeof(Health), typeof(Velocity), typeof(Health)))
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

		const int entityCount = 20;
		for (var i = 0; i < entityCount; i++)
		{
			var entity = world.CreateEntity();
			world.AddComponent(entity, new Position { X = i, Y = i });

			if (i % 2 == 0)
				world.AddComponent(entity, new Velocity { X = 1, Y = 1 });
		}

		// Act
		var count = 0;
		world.Query().With<Position>().Without<Velocity>().ForEachParallel(
			chunk => Interlocked.Add(ref count, chunk.Count), 4
		);

		// Assert
		count.Should().Be(entityCount / 2);
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

		const int entityCount = 128;
		var expectedSum = 0;
		for (var i = 1; i <= entityCount; i++)
		{
			var entity = world.CreateEntity();
			world.AddComponent(entity, new Position { X = i, Y = 0 });
			expectedSum += i;
		}

		var processedCount = 0;
		long processedSum = 0;

		// Act
		world.Query().With<Position>().ForEachParallel(
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
		processedCount.Should().Be(entityCount);
		processedSum.Should().Be(expectedSum);
	}

	[Fact]
	public void AddComponent_WhenCalledDuringQueryIteration_ShouldThrow()
	{
		// Arrange
		var world  = new World();
		var entity = world.CreateEntity();
		world.AddComponent(entity, new Position { X = 1, Y = 1 });

		// Act
		var act = () =>
		{
			foreach (var _ in world.Query().With<Position>())
				world.AddComponent(entity, new Velocity { X = 0, Y = 0 });
		};

		// Assert
		act.Should().Throw<InvalidOperationException>();
		world.HasComponent<Velocity>(entity).Should().BeFalse();
	}

	[Fact]
	public void SetComponent_WhenEntityBelongsToDifferentWorld_ShouldThrow()
	{
		// Arrange
		var worldA  = new World();
		var worldB  = new World();
		var foreign = worldA.CreateEntity();
		var local   = worldB.CreateEntity();

		// Act
		var act = () => worldB.SetComponent(foreign, new Position { X = 5, Y = 6 });

		// Assert
		act.Should().Throw<InvalidOperationException>();
		worldB.HasComponent<Position>(local).Should().BeFalse();
	}

	private struct Health : IComponent;

	private struct Position : IComponent
	{
		public float X;
		public float Y;
	}

	private struct Velocity : IComponent
	{
		public float X;
		public float Y;
	}
}
