using Bezoro.ECS.Services;
using Bezoro.ECS.Types;
using FluentAssertions;
using Xunit;

namespace Bezoro.ECS.Tests.Services;

public partial class WorldRuntimeTests
{
	// ── 4a: Entity lifecycle ─────────────────────────────────────────────────

	[Fact]
	public void IsAlive_WhenEntityDespawned_ShouldReturnFalseForOldHandle()
	{
		using var world = new World(
			new WorldConfig
			{
				EntityCapacity                = 8,
				ComponentTypeCapacity         = 8,
				CommandCapacity               = 16,
				CommandPayloadCapacityPerType = 16,
				QueryResultCapacity           = 8
			}
		);

		var entity = world.Spawn();
		world.IsAlive(entity).Should().BeTrue();

		world.Despawn(entity);
		world.IsAlive(entity).Should().BeFalse();
	}


	[Fact]
	public void IsAlive_WhenEntityRespawnedMultipleTimes_ShouldAlwaysInvalidatePreviousHandle()
	{
		using var world = new World(
			new WorldConfig
			{
				EntityCapacity                = 8,
				ComponentTypeCapacity         = 8,
				CommandCapacity               = 16,
				CommandPayloadCapacityPerType = 16,
				QueryResultCapacity           = 8
			}
		);

		var first = world.Spawn();
		world.Despawn(first);

		var second = world.Spawn();
		world.Despawn(second);

		var third = world.Spawn();
		world.IsAlive(first).Should().BeFalse();
		world.IsAlive(second).Should().BeFalse();
		world.IsAlive(third).Should().BeTrue();
		third.Version.Should().BeGreaterThan(second.Version);
	}

	[Fact]
	public void Reset_WhenCalled_ShouldInvalidatePreviousEntitiesAndClearComponentData()
	{
		using var world = new World(
			new WorldConfig
			{
				EntityCapacity                = 8,
				ComponentTypeCapacity         = 8,
				CommandCapacity               = 16,
				CommandPayloadCapacityPerType = 16,
				QueryResultCapacity           = 8
			}
		);

		using var commands = world.CreateCommandStream();
		var       entity   = commands.CreateEntity();
		commands.Set(entity, new Position { X = 7, Y = 3 });
		world.Playback(commands);

		var query = world.Compile<PositionQuerySpec>();
		using (var beforeReset = world.Execute(query))
		{
			beforeReset.MoveNext().Should().BeTrue();
			beforeReset.Current.Length.Should().Be(1);
		}

		var aliveBeforeReset = query;
		world.Reset();

		using var afterReset = world.Execute(aliveBeforeReset);
		afterReset.MoveNext().Should().BeTrue();
		afterReset.Current.Length.Should().Be(0);
		world.IsAlive(entity).Should().BeFalse();
	}


	[Fact]
	public void Spawn_WhenCalledAfterDespawn_ShouldReuseSlotWithIncrementedVersion()
	{
		using var world = new World(
			new WorldConfig
			{
				EntityCapacity                = 8,
				ComponentTypeCapacity         = 8,
				CommandCapacity               = 16,
				CommandPayloadCapacityPerType = 16,
				QueryResultCapacity           = 8
			}
		);

		var first = world.Spawn();
		world.Despawn(first);

		var second = world.Spawn();
		second.Id.Should().Be(first.Id);
		second.Version.Should().BeGreaterThan(first.Version);
		world.IsAlive(first).Should().BeFalse();
		world.IsAlive(second).Should().BeTrue();
	}
}
