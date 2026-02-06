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
}

[Query]
[All<Position>]
[None<Frozen>]
[Any<Velocity, Acceleration>]
internal readonly partial struct Query;

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
