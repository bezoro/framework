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
	public void UpdateAll_ShouldRespectWriteReadDependenciesAcrossBatches()
	{
		// Arrange
		var world = new World(new() { MaxDegreeOfParallelism = 1 });
		var entity = world.CreateEntity();
		world.AddComponent(entity, new Counter { Value = 1 });

		var preRead  = new ReadCounterSystem();
		var write    = new WriteCounterSystem(2);
		var postRead = new ReadCounterSystem();

		world.RegisterSystem(preRead);
		world.RegisterSystem(write);
		world.RegisterSystem(postRead);

		// Act
		world.Update(1f / 60f);

		// Assert
		preRead.LastObserved.Should().Be(1);
		postRead.LastObserved.Should().Be(2);
	}

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

	private readonly struct Counter : IComponent
	{
		public int Value { get; init; }
	}

	private sealed class ReadCounterSystem : ISystem
	{
		public int LastObserved { get; private set; } = -1;

		public SystemUpdateSettings UpdateSettings => SystemUpdateSettings.EveryFrame;

		public ComponentAccess[] Accesses => [ComponentAccess.Read<Counter>()];

		public void Update(IWorld world, in SystemContext context)
		{
			foreach (var chunk in world.Query().With<Counter>())
			{
				var counters = chunk.Components<Counter>();
				if (chunk.Count > 0)
					LastObserved = counters[0].Value;
			}
		}
	}

	private sealed class WriteCounterSystem : ISystem
	{
		private readonly int _value;

		public WriteCounterSystem(int value)
		{
			_value = value;
		}

		public SystemUpdateSettings UpdateSettings => SystemUpdateSettings.EveryFrame;

		public ComponentAccess[] Accesses => [ComponentAccess.Write<Counter>()];

		public void Update(IWorld world, in SystemContext context)
		{
			foreach (var chunk in world.Query().With<Counter>())
			{
				var counters = chunk.Components<Counter>();
				for (var i = 0; i < chunk.Count; i++)
					counters[i] = new Counter { Value = _value };
			}
		}
	}
}
