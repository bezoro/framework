using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.ECS.Tests.Services;

[TestSubject(typeof(World))]
public class WorldTests
{
	[Fact]
	public void AddComponent_Should_Be_Queryable()
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
	public void Query_Should_Return_All_Entities_With_Position_And_Velocity()
	{
		// Arrange
		var world = new World();
		var e1    = world.CreateEntity();
		world.AddComponent(e1, new Position { X = 1, Y = 1 });
		world.AddComponent(e1, new Velocity { X = 0.5f, Y = 0.25f });

		var e2 = world.CreateEntity();
		world.AddComponent(e2, new Position { X = 2, Y = 2 });
		world.AddComponent(e2, new Velocity { X = 1.5f, Y = -0.5f });

		var e3 = world.CreateEntity();
		world.AddComponent(e3, new Position { X = 3, Y = 3 });

		// Act
		int count = 0;
		foreach (var chunk in world.Query().With<Position>().With<Velocity>())
			count += chunk.Count;

		// Assert
		count.Should().Be(2);
	}

	[Fact]
	public void Query_With_Archetype_Filter_Should_Return_Only_Exact_Archetype()
	{
		// Arrange
		var world = new World();
		var baseArchetype = world.GetOrCreateArchetype(typeof(Position), typeof(Velocity));
		var extendedArchetype = world.GetOrCreateArchetype(typeof(Position), typeof(Velocity), typeof(Health));

		world.CreateEntity(baseArchetype);
		world.CreateEntity(extendedArchetype);

		// Act
		int allCount = 0;
		foreach (var chunk in world.Query().With<Position>().With<Velocity>())
			allCount += chunk.Count;

		int filteredCount = 0;
		foreach (var chunk in world.Query(baseArchetype).With<Position>().With<Velocity>())
			filteredCount += chunk.Count;

		// Assert
		allCount.Should().Be(2);
		filteredCount.Should().Be(1);
	}

	[Fact]
	public void GetOrCreateArchetype_Should_Return_Same_Instance_For_Same_Component_Set()
	{
		// Arrange
		var world = new World();

		// Act
		var first  = world.GetOrCreateArchetype(typeof(Position), typeof(Velocity));
		var second = world.GetOrCreateArchetype(typeof(Velocity), typeof(Position));

		// Assert
		first.Should().BeSameAs(second);
	}

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

	private struct Health : IComponent
	{ }
}
