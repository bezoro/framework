using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.ECS.Tests.Services;

[TestSubject(typeof(CommandBuffer))]
public class CommandBufferTests
{
	[Fact]
	public void CommandBuffer_Should_Defer_Structural_Changes_Until_After_Update()
	{
		// Arrange
		var world  = new World();
		var entity = world.CreateEntity();
		world.AddComponent(entity, new Position { X = 1, Y = 2 });
		world.RegisterSystem(new AddVelocitySystem(entity));

		// Act
		world.HasComponent<Velocity>(entity).Should().BeFalse();
		world.Update(0.016f);

		// Assert
		world.HasComponent<Velocity>(entity).Should().BeTrue();
	}

	private sealed class AddVelocitySystem : ISystem
	{
		private readonly Entity _entity;

		public AddVelocitySystem(Entity entity) => _entity = entity;

		public SystemUpdateSettings UpdateSettings => SystemUpdateSettings.EveryFrame;

		public ComponentAccess[] Accesses => [ComponentAccess.Read<Position>()];

		public void Update(IWorld world, in SystemContext context)
		{
			if (world.HasComponent<Velocity>(_entity)) return;

			context.Commands.AddComponent(_entity, new Velocity { X = 1, Y = 0 });
		}
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
}
