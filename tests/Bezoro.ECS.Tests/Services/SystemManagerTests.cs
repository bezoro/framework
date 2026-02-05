using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.ECS.Tests.Services;

[TestSubject(typeof(SystemManager))]
public class SystemManagerTests
{
	[Fact]
	public void UpdateAll_Should_Respect_Update_Frequency()
	{
		// Arrange
		var world  = new World();
		var system = new FixedStepSystem();
		world.RegisterSystem(system);

		// Act
		world.Update(0.2f);
		world.Update(0.29f);
		world.Update(0.31f);

		// Assert
		system.UpdateCount.Should().Be(1);
		system.LastDeltaTime.Should().BeApproximately(0.5f, 0.0001f);
	}

	[Fact]
	public void UpdateAll_When_DeltaTime_Is_Large_Should_Cap_Catch_Up_Ticks()
	{
		// Arrange
		var world  = new World();
		var system = new FixedStepSystem();
		world.RegisterSystem(system);

		// Act
		world.Update(10f);
		world.Update(0f);
		world.Update(0f);
		world.Update(0f);

		// Assert
		system.UpdateCount.Should().Be(3);
		system.LastDeltaTime.Should().BeApproximately(0.5f, 0.0001f);
	}

	private sealed class FixedStepSystem : ISystem
	{
		public int UpdateCount { get; private set; }
		public float LastDeltaTime { get; private set; }

		public SystemUpdateSettings UpdateSettings => SystemUpdateSettings.Fixed(0.5f);

		public ComponentAccess[] Accesses => [];

		public void Update(IWorld world, in SystemContext context)
		{
			UpdateCount++;
			LastDeltaTime = context.DeltaTime;
		}
	}
}
