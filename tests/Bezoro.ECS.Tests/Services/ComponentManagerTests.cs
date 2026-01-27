using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.ECS.Tests.Services;

[TestSubject(typeof(ComponentManager))]
public class ComponentManagerTests
{
	[Fact]
	public void AddComponent_Should_Associate_Component_With_Entity()
	{
		// Arrange
		var componentManager = new ComponentManager();
		var entity           = new Entity(1);
		var component        = new TestComponent { Value = 42 };

		// Act
		componentManager.AddComponent(entity, component);

		// Assert
		componentManager.HasComponent<TestComponent>(entity).Should().BeTrue();
	}

	[Fact]
	public void GetComponent_Should_Retrieve_Associated_Component()
	{
		// Arrange
		var componentManager  = new ComponentManager();
		var entity            = new Entity(1);
		var expectedComponent = new TestComponent { Value = 42 };
		componentManager.AddComponent(entity, expectedComponent);

		// Act
		var actualComponent = componentManager.GetComponent<TestComponent>(entity);

		// Assert
		actualComponent.Should().Be(expectedComponent);
	}

	[Fact]
	public void RemoveComponent_Should_Disassociate_Component_From_Entity()
	{
		// Arrange
		var componentManager = new ComponentManager();
		var entity           = new Entity(1);
		var component        = new TestComponent { Value = 42 };
		componentManager.AddComponent(entity, component);

		// Act
		componentManager.RemoveComponent<TestComponent>(entity);

		// Assert
		componentManager.HasComponent<TestComponent>(entity).Should().BeFalse();
	}
}

internal struct TestComponent : IComponent
{
	public int Value;
}
