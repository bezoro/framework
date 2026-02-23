using Bezoro.ECS.Services;
using Bezoro.ECS.Types;
using FluentAssertions;
using Xunit;

namespace Bezoro.ECS.Tests.Services;

public partial class WorldRuntimeTests
{
	[Fact]
	public void GetAccessor_Has_WhenComponentRemoved_ShouldReflectStructuralState()
	{
		using var world = new World(
			new WorldConfig
			{
				EntityCapacity                = 32,
				ComponentTypeCapacity         = 16,
				CommandCapacity               = 64,
				CommandPayloadCapacityPerType = 64,
				QueryResultCapacity           = 32
			}
		);

		using var create    = world.CreateCommandStream();
		var       temporary = create.CreateEntity();
		create.Set(temporary, new Position { X = 1, Y = 2 });
		world.Playback(create);

		var    handle = world.Compile<PositionQuerySpec>();
		Entity entity;
		using (var cursor = world.Execute(handle))
		{
			cursor.MoveNext().Should().BeTrue();
			entity = cursor.Current[0];
		}

		var accessor = world.GetAccessor<Position>();
		accessor.Has(entity).Should().BeTrue();

		using var remove = world.CreateCommandStream();
		remove.Remove<Position>(entity);
		world.Playback(remove);

		accessor.Has(entity).Should().BeFalse();
		accessor.TryGet(entity, out _).Should().BeFalse();
	}

	[Fact]
	public void GetAccessor_WhenReadingAndWritingSequentially_ShouldMirrorGetAndTryGet()
	{
		using var world = new World(
			new WorldConfig
			{
				EntityCapacity                = 64,
				ComponentTypeCapacity         = 16,
				CommandCapacity               = 128,
				CommandPayloadCapacityPerType = 128,
				QueryResultCapacity           = 64
			}
		);

		using var commands = world.CreateCommandStream();
		for (var i = 0; i < 16; i++)
		{
			var entity = commands.CreateEntity();
			commands.Set(entity, new Position { X = i, Y = -i });
		}

		world.Playback(commands);
		var handle   = world.Compile<PositionQuerySpec>();
		var entities = new Entity[16];
		using (var cursor = world.Execute(handle))
		{
			cursor.MoveNext().Should().BeTrue();
			cursor.Current.CopyTo(entities);
		}

		var accessor = world.GetAccessor<Position>();
		for (var i = 0; i < entities.Length; i++)
		{
			accessor.TryGet(entities[i], out var position).Should().BeTrue();
			position.X.Should().Be(i);
			position.Y.Should().Be(-i);

			ref var writable = ref accessor.Get(entities[i]);
			writable.X += 10;
			writable.Y -= 5;
		}

		for (var i = 0; i < entities.Length; i++)
		{
			var updated = world.Get<Position>(entities[i]);
			updated.X.Should().Be(i + 10);
			updated.Y.Should().Be(-i - 5);
		}
	}


	[Fact]
	public void GetAccessor_WhenSwitchingAcrossArchetypes_ShouldResolvePresencePerEntityWithoutStaleCache()
	{
		using var world = new World(
			new WorldConfig
			{
				EntityCapacity                = 32,
				ComponentTypeCapacity         = 16,
				CommandCapacity               = 64,
				CommandPayloadCapacityPerType = 64,
				QueryResultCapacity           = 32
			}
		);

		using var create       = world.CreateCommandStream();
		var       withVelocity = create.CreateEntity();
		create.Set(withVelocity, new Position { X = 1, Y = 1 });
		create.Set(withVelocity, new Velocity { X = 7, Y = -3 });

		var withoutVelocity = create.CreateEntity();
		create.Set(withoutVelocity, new Position { X = 2, Y = 2 });
		world.Playback(create);

		var    positionHandle          = world.Compile<PositionQuerySpec>();
		Entity resolvedWithVelocity    = default;
		Entity resolvedWithoutVelocity = default;
		using (var cursor = world.Execute(positionHandle))
		{
			cursor.MoveNext().Should().BeTrue();
			for (var i = 0; i < cursor.Current.Length; i++)
			{
				var entity = cursor.Current[i];
				if (world.Has<Velocity>(entity))
					resolvedWithVelocity = entity;
				else
					resolvedWithoutVelocity = entity;
			}
		}

		var accessor = world.GetAccessor<Velocity>();
		accessor.Has(resolvedWithVelocity).Should().BeTrue();
		accessor.TryGet(resolvedWithVelocity, out var velocity).Should().BeTrue();
		velocity.Should().Be(new Velocity { X = 7, Y = -3 });

		accessor.Has(resolvedWithoutVelocity).Should().BeFalse();
		accessor.TryGet(resolvedWithoutVelocity, out _).Should().BeFalse();

		accessor.Has(resolvedWithVelocity).Should().BeTrue();
		accessor.TryGet(resolvedWithVelocity, out velocity).Should().BeTrue();
		velocity.Should().Be(new Velocity { X = 7, Y = -3 });
	}
}
