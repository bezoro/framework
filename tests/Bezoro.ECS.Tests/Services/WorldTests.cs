using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Services;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.ECS.Tests.Services;

[TestSubject(typeof(World))]
public class WorldTests
{
	[Fact]
	public void AddAndGetComponent_Should_Correctly_Manage_Components()
	{
		// Arrange
		var world     = new World();
		var entity    = world.CreateEntity();
		var component = new TestComponent { Value = 123 };

		// Act
		world.AddComponent(entity, component);
		var retrievedComponent = world.GetComponent<TestComponent>(entity);

		// Assert
		world.HasComponent<TestComponent>(entity).Should().BeTrue();
		retrievedComponent.Value.Should().Be(component.Value);
	}

	[Fact]
	public void CreateEntity_Should_Return_A_Valid_Entity()
	{
		// Arrange
		var world = new World();

		// Act
		var entity1 = world.CreateEntity();
		var entity2 = world.CreateEntity();

		// Assert
		entity1.Should().NotBe(entity2);
	}

	[Fact]
	public void DestroyEntity_Should_Remove_Entity_And_Its_Components()
	{
		// Arrange
		var world  = new World();
		var entity = world.CreateEntity();
		world.AddComponent(entity, new TestComponent { Value = 42 });

		// Act
		world.DestroyEntity(entity);

		// Assert
		world.HasComponent<TestComponent>(entity).Should().BeFalse();
	}

	[Fact]
	public void Update_Should_Call_Update_On_Registered_Systems()
	{
		// Arrange
		var world      = new World();
		var testSystem = new TestSystem();
		world.RegisterSystem(testSystem);

		// Act
		world.Update();

		// Assert
		testSystem.WasUpdated.Should().BeTrue();
	}

	private struct TestComponent : IComponent
	{
		public int Value;
	}

	private class TestSystem : ISystem
	{
		public bool WasUpdated { get; private set; }

		#region Interface Implementations

		public void Update() => WasUpdated = true;

		#endregion
	}
}
