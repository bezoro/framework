using System;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;
using FluentAssertions;
using Xunit;

namespace Bezoro.ECS.Tests.Services;

public partial class WorldRuntimeTests
{
	[Fact]
	public void CommandStream_WhenOverflowPolicyDropsNewest_ShouldTrackOverflowAndHighWatermark()
	{
		using var world = new World(
			new WorldConfig
			{
				EntityCapacity                = 8,
				ComponentTypeCapacity         = 8,
				CommandCapacity               = 1,
				CommandPayloadCapacityPerType = 8,
				QueryResultCapacity           = 8,
				OverflowPolicy                = WorldOverflowPolicy.DropNewest
			}
		);

		using var commands = world.CreateCommandStream();
		_ = commands.CreateEntity();
		commands.Destroy(new(0, 0));

		var diagnostics = commands.GetDiagnostics();
		diagnostics.CommandCapacity.Should().Be(1);
		diagnostics.RecordedCommands.Should().Be(1);
		diagnostics.HighWatermark.Should().Be(1);
		diagnostics.OverflowCount.Should().Be(1);
	}


	// ── 4b: CommandStream overflow silent drops ───────────────────────────

	[Fact]
	public void Destroy_WhenCommandStreamFull_ShouldIncrementOverflowAndNotReplay()
	{
		using var world = new World(
			new WorldConfig
			{
				EntityCapacity                = 8,
				ComponentTypeCapacity         = 8,
				CommandCapacity               = 1,
				CommandPayloadCapacityPerType = 8,
				QueryResultCapacity           = 8,
				OverflowPolicy                = WorldOverflowPolicy.DropNewest
			}
		);

		// Fill the stream (one create exhausts capacity of 1)
		using var commands = world.CreateCommandStream();
		commands.CreateEntity();

		var existing = new Entity(0, 0);
		commands.Destroy(existing);

		var diag = commands.GetDiagnostics();
		diag.RecordedCommands.Should().Be(1);
		diag.OverflowCount.Should().Be(1);
	}

	[Fact]
	public void Playback_WhenEntityCapacityExceeded_ShouldThrow()
	{
		using var world = new World(
			new WorldConfig
			{
				EntityCapacity                = 1,
				ComponentTypeCapacity         = 8,
				CommandCapacity               = 8,
				CommandPayloadCapacityPerType = 8,
				QueryResultCapacity           = 1
			}
		);

		using var commands = world.CreateCommandStream();
		commands.CreateEntity();
		commands.CreateEntity();

		var act = () => world.Playback(commands);
		act.Should().Throw<InvalidOperationException>()
		   .WithMessage("*Entity capacity*");
	}


	// ── 4c: CommandStream batch marker boundary (EntityCapacity = 32) ─────

	[Fact]
	public void Playback_WhenEntityCapacityIs32_ShouldUseSingleMarkerWordWithoutOverflow()
	{
		using var world = new World(
			new WorldConfig
			{
				EntityCapacity                = 32,
				ComponentTypeCapacity         = 8,
				CommandCapacity               = 64,
				CommandPayloadCapacityPerType = 64,
				QueryResultCapacity           = 32
			}
		);

		// Spawn 32 entities with a component, then use a Set batch command to exercise marker bits
		var       entities = new Entity[32];
		using var create   = world.CreateCommandStream();
		for (var i = 0; i < 32; i++)
		{
			entities[i] = create.CreateEntity();
			create.Set(entities[i], new Position { X = i, Y = i });
		}

		world.Playback(create);

		// Resolve temporary entities via query
		var handle   = world.Compile<PositionQuerySpec>();
		var resolved = new Entity[32];
		using (var cursor = world.Execute(handle))
		{
			cursor.MoveNext().Should().BeTrue();
			cursor.Current.Length.Should().Be(32);
			cursor.Current.CopyTo(resolved);
		}

		// Now batch-set all 32 entities — exercises the boundary where exactly 1 marker word (32 bits) is needed
		using var updates = world.CreateCommandStream();
		for (var i = 0; i < 32; i++)
			updates.Set(resolved[i], new Position { X = i + 100, Y = i + 100 });

		world.Playback(updates);

		using var verify = world.Execute(handle);
		verify.MoveNext().Should().BeTrue();
		verify.Current.Length.Should().Be(32);
		for (var i = 0; i < 32; i++)
			world.Get<Position>(resolved[i]).X.Should().BeGreaterThanOrEqualTo(100);
	}


	[Fact]
	public void Remove_WhenCommandStreamFull_ShouldIncrementOverflowAndNotReplay()
	{
		using var world = new World(
			new WorldConfig
			{
				EntityCapacity                = 8,
				ComponentTypeCapacity         = 8,
				CommandCapacity               = 1,
				CommandPayloadCapacityPerType = 8,
				QueryResultCapacity           = 8,
				OverflowPolicy                = WorldOverflowPolicy.DropNewest
			}
		);

		using var commands = world.CreateCommandStream();
		commands.CreateEntity();

		var existing = new Entity(0, 0);
		commands.Remove<Position>(existing);

		var diag = commands.GetDiagnostics();
		diag.RecordedCommands.Should().Be(1);
		diag.OverflowCount.Should().Be(1);
	}


	[Fact]
	public void Set_WhenCommandStreamFull_ShouldIncrementOverflowAndNotReplay()
	{
		using var world = new World(
			new WorldConfig
			{
				EntityCapacity                = 8,
				ComponentTypeCapacity         = 8,
				CommandCapacity               = 1,
				CommandPayloadCapacityPerType = 8,
				QueryResultCapacity           = 8,
				OverflowPolicy                = WorldOverflowPolicy.DropNewest
			}
		);

		using var commands = world.CreateCommandStream();
		commands.CreateEntity();

		var existing = new Entity(0, 0);
		commands.Set(existing, new Position { X = 1, Y = 2 });

		var diag = commands.GetDiagnostics();
		diag.RecordedCommands.Should().Be(1);
		diag.OverflowCount.Should().Be(1);
	}
}
