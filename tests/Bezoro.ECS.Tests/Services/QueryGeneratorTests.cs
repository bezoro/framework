using Bezoro.ECS.Attributes;
using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Services;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.ECS.Tests.Services;

[TestSubject(typeof(QueryGenerated))]
public class QueryGeneratorTests
{
	[Fact]
	public void GeneratedQueryCreate_WhenUsingAttributeFilters_ShouldMatchExpectedEntities()
	{
		var world = new World();

		var e1 = world.Spawn();
		world.Add(e1, new Position { X = 1, Y = 1 });
		world.Add(e1, new Velocity { X = 2, Y = 0 });

		var e2 = world.Spawn();
		world.Add(e2, new Position { X = 1, Y = 1 });
		world.Add(e2, new Acceleration { X = 1, Y = 1 });

		var e3 = world.Spawn();
		world.Add(e3, new Position { X = 1, Y = 1 });
		world.Add(e3, new Velocity { X = 1, Y = 1 });
		world.Add(e3, new Frozen());

		var query = QueryGenerated.Create(world);
		var count = 0;
		foreach (var chunk in query)
			count += chunk.Count;

		count.Should().Be(2);
	}

	[Fact]
	public void GeneratedQueryForEach_WhenUsingTypedDelegate_ShouldApplyComponentUpdates()
	{
		var world = new World();

		var entity = world.Spawn();
		world.Add(entity, new Position { X = 1, Y = 2 });
		world.Add(entity, new Velocity { X = 3, Y = 4 });

		QueryGenerated.ForEach(world, (ref Position position, in Velocity velocity) =>
		{
			position.X += velocity.X;
			position.Y += velocity.Y;
		});

		var updated = world.Get<Position>(entity);
		updated.X.Should().Be(4);
		updated.Y.Should().Be(6);
	}

	[Fact]
	public void GeneratedQueryForEachRw_WhenUsingTwoWritableComponents_ShouldApplyMutations()
	{
		var world = new World();

		var entity = world.Spawn();
		world.Add(entity, new Position { X = 1, Y = 2 });
		world.Add(entity, new Velocity { X = 3, Y = 4 });

		QueryGenerated.ForEachRW(world, (ref Position position, ref Velocity velocity) =>
		{
			position.X += 10;
			velocity.Y -= 1;
		});

		var updatedPosition = world.Get<Position>(entity);
		var updatedVelocity = world.Get<Velocity>(entity);
		updatedPosition.X.Should().Be(11);
		updatedVelocity.Y.Should().Be(3);
	}

	[Fact]
	public void GeneratedForEachJobExtension_WhenUsingJobStruct_ShouldApplyUpdatesWithoutGenericArguments()
	{
		var world = new World();
		var entity = world.Spawn();
		world.Add(entity, new Position { X = 1, Y = 2 });
		world.Add(entity, new Velocity { X = 3, Y = 4 });

		world.Query<Position, Velocity>().ForEach(new MovementForEachJob { DeltaTime = 0.5f });

		var updated = world.Get<Position>(entity);
		updated.X.Should().Be(2.5f);
		updated.Y.Should().Be(4f);
	}

	[Fact]
	public void GeneratedForEachJobExtension_WhenUsingNestedJobStruct_ShouldApplyUpdatesWithoutGenericArguments()
	{
		var world = new World();
		var entity = world.Spawn();
		world.Add(entity, new Position { X = 2, Y = 3 });
		world.Add(entity, new Velocity { X = 1, Y = -2 });

		world.Query<Position, Velocity>().ForEach(new JobContainer.NestedMovementForEachJob { DeltaTime = 2f });

		var updated = world.Get<Position>(entity);
		updated.X.Should().Be(4f);
		updated.Y.Should().Be(-1f);
	}

	[Fact]
	public void GeneratedQuery_WhenUsingWorldQueryDefinitionEntryPoint_ShouldMatchExpectedEntities()
	{
		var world = new World();

		var e1 = world.Spawn();
		world.Add(e1, new Position { X = 1, Y = 1 });
		world.Add(e1, new Velocity { X = 2, Y = 0 });

		var e2 = world.Spawn();
		world.Add(e2, new Position { X = 1, Y = 1 });
		world.Add(e2, new Acceleration { X = 1, Y = 1 });

		var e3 = world.Spawn();
		world.Add(e3, new Position { X = 1, Y = 1 });
		world.Add(e3, new Velocity { X = 1, Y = 1 });
		world.Add(e3, new Frozen());

		var count = 0;
		foreach (var chunk in world.Query<Query>())
			count += chunk.Count;

		count.Should().Be(2);
	}

	[Fact]
	public void GeneratedQuery_WhenQueryStructOmitsIQuery_ShouldStillSupportWorldEntryPoint()
	{
		var world = new World();
		world.Spawn(new Position { X = 1, Y = 1 });
		world.Spawn();

		var count = 0;
		foreach (var chunk in world.Query<AutoQuery>())
			count += chunk.Count;

		count.Should().Be(1);
	}
}

[Query]
[All<Position>]
[None<Frozen>]
[Any<Velocity, Acceleration>]
internal readonly partial struct Query : IQuery;

[Query]
[All<Position>]
internal readonly partial struct AutoQuery;

internal struct Position : IComponent
{
	public float X;
	public float Y;
}

internal struct Velocity : IComponent
{
	public float X;
	public float Y;
}

internal struct Acceleration : IComponent
{
	public float X;
	public float Y;
}

internal struct Frozen : IComponent;

internal struct MovementForEachJob : IForEach<Position, Velocity>
{
	public float DeltaTime;

	public void Execute(ref Position component1, in Velocity component2)
	{
		component1.X += component2.X * DeltaTime;
		component1.Y += component2.Y * DeltaTime;
	}
}

internal static class JobContainer
{
	internal struct NestedMovementForEachJob : IForEach<Position, Velocity>
	{
		public float DeltaTime;

		public void Execute(ref Position component1, in Velocity component2)
		{
			component1.X += component2.X * DeltaTime;
			component1.Y += component2.Y * DeltaTime;
		}
	}
}
