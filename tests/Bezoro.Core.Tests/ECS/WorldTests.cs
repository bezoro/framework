using Bezoro.Core.ECS;
using Xunit;

namespace Bezoro.Core.Tests.ECS;

public class WorldTests
{
	[Fact]
	public void AddAndGetComponent_Should_Correctly_Manage_Components()
	{
		// Arrange
		var    world     = new World();
		Entity entity    = world.CreateEntity();
		var    component = new TestComponent { Value = 123 };

		// Act
		world.AddComponent(entity, component);
		var retrievedComponent = world.GetComponent<TestComponent>(entity);

		// Assert
		Assert.True(world.HasComponent<TestComponent>(entity));
		Assert.Equal(component.Value, retrievedComponent.Value);
	}

	[Fact]
	public void CreateEntity_Should_Return_A_Valid_Entity()
	{
		// Arrange
		var world = new World();

		// Act
		Entity entity1 = world.CreateEntity();
		Entity entity2 = world.CreateEntity();

		// Assert
		Assert.NotEqual(entity1, entity2);
	}

	[Fact]
	public void DestroyEntity_Should_Remove_Entity_And_Its_Components()
	{
		// Arrange
		var    world  = new World();
		Entity entity = world.CreateEntity();
		world.AddComponent(entity, new TestComponent { Value = 42 });

		// Act
		world.DestroyEntity(entity);

		// Assert
		Assert.False(world.HasComponent<TestComponent>(entity));
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
		Assert.True(testSystem.WasUpdated);
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
