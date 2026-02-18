using System;
using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.ECS.Tests.Services;

[TestSubject(typeof(World))]
public class WorldRuntimeTests
{
	[Fact]
	public void Playback_WhenCommandStreamCreatesTemporaryEntity_ShouldResolveAndApplyComponents()
	{
		using var world = new World(new WorldConfig()
		{
			EntityCapacity                = 32,
			ComponentTypeCapacity         = 16,
			CommandCapacity               = 64,
			CommandPayloadCapacityPerType = 64,
			QueryResultCapacity           = 32
		});

		using var commands = world.CreateCommandStream();
		var temporary = commands.CreateEntity();
		commands.Set(temporary, new Position { X = 4, Y = 9 });

		world.Playback(commands);

		var query = world.Compile<PositionQuerySpec>();
		using var cursor = world.Execute(query);
		cursor.MoveNext().Should().BeTrue();
		cursor.Current.Length.Should().Be(1);
		cursor.Get<Position>(0).Should().Be(new Position { X = 4, Y = 9 });
	}

	[Fact]
	public void Playback_WhenCreateEntityWithComponentIsUsed_ShouldCreateEntityInSingleCommand()
	{
		using var world = new World(new WorldConfig()
		{
			EntityCapacity                = 16,
			ComponentTypeCapacity         = 16,
			CommandCapacity               = 16,
			CommandPayloadCapacityPerType = 16,
			QueryResultCapacity           = 16
		});

		using var commands = world.CreateCommandStream();
		commands.CreateEntity(new Position { X = 11, Y = -3 });
		commands.GetDiagnostics().RecordedCommands.Should().Be(1);

		world.Playback(commands);

		var handle = world.Compile<PositionQuerySpec>();
		using var cursor = world.Execute(handle);
		cursor.MoveNext().Should().BeTrue();
		cursor.Current.Length.Should().Be(1);
		cursor.Get<Position>(0).Should().Be(new Position { X = 11, Y = -3 });
	}

	[Fact]
	public void Execute_WhenCompiledQueryUsesAllAndNone_ShouldReturnExpectedEntities()
	{
		using var world = new World(new WorldConfig()
		{
			EntityCapacity                = 64,
			ComponentTypeCapacity         = 16,
			CommandCapacity               = 128,
			CommandPayloadCapacityPerType = 128,
			QueryResultCapacity           = 64
		});

		using var commands = world.CreateCommandStream();
		var first = commands.CreateEntity();
		commands.Set(first, new Position { X = 1, Y = 1 });

		var second = commands.CreateEntity();
		commands.Set(second, new Position { X = 2, Y = 2 });
		commands.Set(second, new Velocity { X = 9, Y = 9 });
		world.Playback(commands);

		var query = world.Compile<PositionWithoutVelocityQuerySpec>();
		using var cursor = world.Execute(query);
		cursor.MoveNext().Should().BeTrue();
		cursor.Current.Length.Should().Be(1);
		cursor.Get<Position>(0).Should().Be(new Position { X = 1, Y = 1 });
	}

	[Fact]
	public void Execute_WhenCompiledQueryIsCachedBeforeMatchingArchetypeExists_ShouldRefreshAfterStructuralChange()
	{
		using var world = new World(new WorldConfig()
		{
			EntityCapacity                = 32,
			ComponentTypeCapacity         = 16,
			CommandCapacity               = 64,
			CommandPayloadCapacityPerType = 64,
			QueryResultCapacity           = 32
		});

		var handle = world.Compile<PositionQuerySpec>();
		using (var before = world.Execute(handle))
		{
			before.MoveNext().Should().BeTrue();
			before.Current.Length.Should().Be(0);
		}

		using var commands = world.CreateCommandStream();
		var created = commands.CreateEntity();
		commands.Set(created, new Position { X = 42, Y = -7 });
		world.Playback(commands);

		using var after = world.Execute(handle);
		after.MoveNext().Should().BeTrue();
		after.Current.Length.Should().Be(1);
		world.Get<Position>(after.Current[0]).Should().Be(new Position { X = 42, Y = -7 });
	}

	[Fact]
	public void Compile_WhenDifferentSpecTypesAreCached_ShouldKeepDistinctQueryPlans()
	{
		using var world = new World(new WorldConfig()
		{
			EntityCapacity                = 16,
			ComponentTypeCapacity         = 16,
			CommandCapacity               = 32,
			CommandPayloadCapacityPerType = 32,
			QueryResultCapacity           = 16
		});

		using var commands = world.CreateCommandStream();
		var entity = commands.CreateEntity();
		commands.Set(entity, new Position { X = 1, Y = 1 });
		world.Playback(commands);

		var positionOnly = world.Compile<PositionQuerySpec>();
		var positionAndVelocity = world.Compile<PositionAndVelocityQuerySpec>();

		using (var positionCursor = world.Execute(positionOnly))
		{
			positionCursor.MoveNext().Should().BeTrue();
			positionCursor.Current.Length.Should().Be(1);
		}

		using var positionVelocityCursor = world.Execute(positionAndVelocity);
		positionVelocityCursor.MoveNext().Should().BeTrue();
		positionVelocityCursor.Current.Length.Should().Be(0);
	}

	[Fact]
	public void QueryCursor_ForEach_WhenMutatingSequentially_ShouldUpdateComponentsInPlace()
	{
		using var world = new World(new WorldConfig()
		{
			EntityCapacity                = 16,
			ComponentTypeCapacity         = 16,
			CommandCapacity               = 32,
			CommandPayloadCapacityPerType = 32,
			QueryResultCapacity           = 16
		});

		using var commands = world.CreateCommandStream();
		for (var i = 0; i < 3; i++)
		{
			var entity = commands.CreateEntity();
			commands.Set(entity, new Position { X = i, Y = i });
			commands.Set(entity, new Velocity { X = 10, Y = -2 });
		}

		world.Playback(commands);

		var handle = world.Compile<PositionAndVelocityQuerySpec>();
		using var cursor = world.Execute(handle);
		cursor.MoveNext().Should().BeTrue();
		cursor.ForEach<Position, Velocity>((ref Position position, in Velocity velocity) =>
		{
			position.X += velocity.X;
			position.Y += velocity.Y;
		});

		for (var i = 0; i < cursor.Current.Length; i++)
		{
			var updated = cursor.Get<Position>(i);
			updated.X.Should().Be(i + 10);
			updated.Y.Should().Be(i - 2);
		}
	}

	[Fact]
	public void QueryCursor_Get_WhenResultSpansMultipleChunks_ShouldResolveEachEntityIndex()
	{
		using var world = new World(new WorldConfig()
		{
			EntityCapacity                = 16,
			ComponentTypeCapacity         = 16,
			CommandCapacity               = 32,
			CommandPayloadCapacityPerType = 32,
			QueryResultCapacity           = 16,
			ChunkCapacity                 = 2
		});

		using var commands = world.CreateCommandStream();
		for (var i = 0; i < 5; i++)
		{
			var entity = commands.CreateEntity();
			commands.Set(entity, new Position { X = i, Y = i });
		}

		world.Playback(commands);

		var handle = world.Compile<PositionQuerySpec>();
		using var cursor = world.Execute(handle);
		cursor.MoveNext().Should().BeTrue();
		cursor.Current.Length.Should().Be(5);

		var seen = new bool[5];
		for (var i = 0; i < cursor.Current.Length; i++)
		{
			var position = cursor.Get<Position>(i);
			int index = (int)position.X;
			index.Should().BeInRange(0, 4);
			seen[index] = true;
		}

		seen.Should().OnlyContain(static value => value);
	}

	[Fact]
	public void QueryCursor_Get_WhenSwitchingTypesAndTraversingBackward_ShouldResolveCorrectComponents()
	{
		using var world = new World(new WorldConfig()
		{
			EntityCapacity                = 32,
			ComponentTypeCapacity         = 16,
			CommandCapacity               = 96,
			CommandPayloadCapacityPerType = 96,
			QueryResultCapacity           = 32,
			ChunkCapacity                 = 2
		});

		using var commands = world.CreateCommandStream();
		for (var i = 0; i < 3; i++)
		{
			var entity = commands.CreateEntity();
			commands.Set(entity, new Position { X = 100 + i, Y = i });
			commands.Set(entity, new Velocity { X = 10 + i, Y = -10 - i });
		}

		for (var i = 0; i < 3; i++)
		{
			var entity = commands.CreateEntity();
			commands.Set(entity, new Position { X = 200 + i, Y = i });
			commands.Set(entity, new Velocity { X = 20 + i, Y = -20 - i });
			commands.Set(entity, new Acceleration { X = 1, Y = 1 });
		}

		world.Playback(commands);

		var handle = world.Compile<PositionAndVelocityQuerySpec>();
		using var cursor = world.Execute(handle);
		cursor.MoveNext().Should().BeTrue();
		cursor.Current.Length.Should().Be(6);

		int[] indexOrder = [0, 1, 2, 3, 2, 1, 4, 5, 4, 0];
		for (var i = 0; i < indexOrder.Length; i++)
		{
			int index = indexOrder[i];
			var entity = cursor.Current[index];

			ref var velocity = ref cursor.Get<Velocity>(index);
			var expectedVelocity = world.Get<Velocity>(entity);
			velocity.X.Should().Be(expectedVelocity.X);
			velocity.Y.Should().Be(expectedVelocity.Y);

			ref var position = ref cursor.Get<Position>(index);
			var expectedPosition = world.Get<Position>(entity);
			position.X.Should().Be(expectedPosition.X);
			position.Y.Should().Be(expectedPosition.Y);
		}
	}

	[Fact]
	public void QueryCursor_ForEach3_WhenMutatingWithTwoReadonlyInputs_ShouldUpdateComponentsInPlace()
	{
		using var world = new World(new WorldConfig()
		{
			EntityCapacity                = 16,
			ComponentTypeCapacity         = 16,
			CommandCapacity               = 48,
			CommandPayloadCapacityPerType = 48,
			QueryResultCapacity           = 16
		});

		using var commands = world.CreateCommandStream();
		for (var i = 0; i < 3; i++)
		{
			var entity = commands.CreateEntity();
			commands.Set(entity, new Position { X = i, Y = i });
			commands.Set(entity, new Velocity { X = 2, Y = 3 });
			commands.Set(entity, new Acceleration { X = 1, Y = -1 });
		}

		world.Playback(commands);

		var handle = world.Compile<PositionVelocityAccelerationQuerySpec>();
		using var cursor = world.Execute(handle);
		cursor.MoveNext().Should().BeTrue();
		cursor.ForEach<Position, Velocity, Acceleration>(
			(ref Position position, in Velocity velocity, in Acceleration acceleration) =>
			{
				position.X += velocity.X + acceleration.X;
				position.Y += velocity.Y + acceleration.Y;
			}
		);

		for (var i = 0; i < cursor.Current.Length; i++)
		{
			var updated = cursor.Get<Position>(i);
			updated.X.Should().Be(i + 3);
			updated.Y.Should().Be(i + 2);
		}
	}

	[Fact]
	public void ForEach_WhenUsingCompiledHandleDirectly_ShouldMutateWithoutCursorMaterialization()
	{
		using var world = new World(new WorldConfig()
		{
			EntityCapacity                = 16,
			ComponentTypeCapacity         = 16,
			CommandCapacity               = 48,
			CommandPayloadCapacityPerType = 48,
			QueryResultCapacity           = 16
		});

		using var commands = world.CreateCommandStream();
		for (var i = 0; i < 3; i++)
		{
			var entity = commands.CreateEntity();
			commands.Set(entity, new Position { X = i, Y = i });
			commands.Set(entity, new Velocity { X = 5, Y = -1 });
		}

		world.Playback(commands);

		var handle = world.Compile<PositionAndVelocityQuerySpec>();
		world.ForEach(handle, (ref Position position, in Velocity velocity) =>
		{
			position.X += velocity.X;
			position.Y += velocity.Y;
		});

		using var cursor = world.Execute(handle);
		cursor.MoveNext().Should().BeTrue();
		for (var i = 0; i < cursor.Current.Length; i++)
		{
			var updated = cursor.Get<Position>(i);
			updated.X.Should().Be(i + 5);
			updated.Y.Should().Be(i - 1);
		}
	}

	[Fact]
	public void ForEach_WhenCursorIsActive_ShouldThrow()
	{
		using var world = new World(new WorldConfig()
		{
			EntityCapacity                = 8,
			ComponentTypeCapacity         = 8,
			CommandCapacity               = 16,
			CommandPayloadCapacityPerType = 16,
			QueryResultCapacity           = 8
		});

		using var commands = world.CreateCommandStream();
		var entity = commands.CreateEntity();
		commands.Set(entity, new Position { X = 1, Y = 1 });
		commands.Set(entity, new Velocity { X = 1, Y = 1 });
		world.Playback(commands);

		var handle = world.Compile<PositionAndVelocityQuerySpec>();
		using var cursor = world.Execute(handle);
		cursor.MoveNext().Should().BeTrue();

		var act = () => world.ForEach(handle, (ref Position position, in Velocity velocity) =>
		{
			position.X += velocity.X;
		});

		act.Should().Throw<InvalidOperationException>()
		   .WithMessage("*query cursor is active*");
	}

	[Fact]
	public void QueryCursor_Run_WhenUsingStructJob_ShouldMutateComponents()
	{
		using var world = new World(new WorldConfig()
		{
			EntityCapacity                = 16,
			ComponentTypeCapacity         = 16,
			CommandCapacity               = 48,
			CommandPayloadCapacityPerType = 48,
			QueryResultCapacity           = 16
		});

		using var commands = world.CreateCommandStream();
		for (var i = 0; i < 3; i++)
		{
			var entity = commands.CreateEntity();
			commands.Set(entity, new Position { X = i, Y = i });
			commands.Set(entity, new Velocity { X = 2, Y = -1 });
		}

		world.Playback(commands);

		var handle = world.Compile<PositionAndVelocityQuerySpec>();
		using var cursor = world.Execute(handle);
		cursor.MoveNext().Should().BeTrue();

		cursor.Run<IntegrateJob, Position, Velocity>(new(3f));

		for (var i = 0; i < cursor.Current.Length; i++)
		{
			var updated = cursor.Get<Position>(i);
			updated.X.Should().Be(i + 6);
			updated.Y.Should().Be(i - 3);
		}
	}

	[Fact]
	public void Run_WhenUsingCompiledHandleAndStructJob_ShouldMutateComponents()
	{
		using var world = new World(new WorldConfig()
		{
			EntityCapacity                = 16,
			ComponentTypeCapacity         = 16,
			CommandCapacity               = 48,
			CommandPayloadCapacityPerType = 48,
			QueryResultCapacity           = 16
		});

		using var commands = world.CreateCommandStream();
		for (var i = 0; i < 3; i++)
		{
			var entity = commands.CreateEntity();
			commands.Set(entity, new Position { X = i, Y = i });
			commands.Set(entity, new Velocity { X = 1, Y = 2 });
		}

		world.Playback(commands);

		var handle = world.Compile<PositionAndVelocityQuerySpec>();
		world.Run<PositionAndVelocityQuerySpec, IntegrateJob, Position, Velocity>(handle, new(2f));

		using var cursor = world.Execute(handle);
		cursor.MoveNext().Should().BeTrue();
		for (var i = 0; i < cursor.Current.Length; i++)
		{
			var updated = cursor.Get<Position>(i);
			updated.X.Should().Be(i + 2);
			updated.Y.Should().Be(i + 4);
		}
	}

	[Fact]
	public void GetAccessor_WhenReadingAndWritingSequentially_ShouldMirrorGetAndTryGet()
	{
		using var world = new World(new WorldConfig()
		{
			EntityCapacity                = 64,
			ComponentTypeCapacity         = 16,
			CommandCapacity               = 128,
			CommandPayloadCapacityPerType = 128,
			QueryResultCapacity           = 64
		});

		using var commands = world.CreateCommandStream();
		for (var i = 0; i < 16; i++)
		{
			var entity = commands.CreateEntity();
			commands.Set(entity, new Position { X = i, Y = -i });
		}

		world.Playback(commands);
		var handle = world.Compile<PositionQuerySpec>();
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
	public void GetAccessor_WhenExecutingRepeatedlyAfterWarmup_ShouldNotAllocate()
	{
		using var world = new World(new WorldConfig()
		{
			EntityCapacity                = 512,
			ComponentTypeCapacity         = 16,
			CommandCapacity               = 2048,
			CommandPayloadCapacityPerType = 2048,
			QueryResultCapacity           = 512
		});

		using var commands = world.CreateCommandStream();
		for (var i = 0; i < 256; i++)
		{
			var entity = commands.CreateEntity();
			commands.Set(entity, new Position { X = i, Y = i });
		}

		world.Playback(commands);
		var handle = world.Compile<PositionQuerySpec>();
		var entities = new Entity[256];
		using (var cursor = world.Execute(handle))
		{
			cursor.MoveNext().Should().BeTrue();
			cursor.Current.CopyTo(entities);
		}

		var accessor = world.GetAccessor<Position>();
		for (var i = 0; i < entities.Length; i++)
		{
			ref var position = ref accessor.Get(entities[i]);
			position.X += 1;
		}

		GC.Collect();
		GC.WaitForPendingFinalizers();
		GC.Collect();

		long before = GC.GetAllocatedBytesForCurrentThread();
		for (var iteration = 0; iteration < 150; iteration++)
		{
			for (var i = 0; i < entities.Length; i++)
			{
				ref var position = ref accessor.Get(entities[i]);
				position.Y -= 1;
			}
		}

		long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
		allocated.Should().Be(0);
	}

	[Fact]
	public void GetAccessor_Has_WhenComponentRemoved_ShouldReflectStructuralState()
	{
		using var world = new World(new WorldConfig()
		{
			EntityCapacity                = 32,
			ComponentTypeCapacity         = 16,
			CommandCapacity               = 64,
			CommandPayloadCapacityPerType = 64,
			QueryResultCapacity           = 32
		});

		using var create = world.CreateCommandStream();
		var temporary = create.CreateEntity();
		create.Set(temporary, new Position { X = 1, Y = 2 });
		world.Playback(create);

		var handle = world.Compile<PositionQuerySpec>();
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
	public void GetAccessor_WhenSwitchingAcrossArchetypes_ShouldResolvePresencePerEntityWithoutStaleCache()
	{
		using var world = new World(new WorldConfig()
		{
			EntityCapacity                = 32,
			ComponentTypeCapacity         = 16,
			CommandCapacity               = 64,
			CommandPayloadCapacityPerType = 64,
			QueryResultCapacity           = 32
		});

		using var create = world.CreateCommandStream();
		var withVelocity = create.CreateEntity();
		create.Set(withVelocity, new Position { X = 1, Y = 1 });
		create.Set(withVelocity, new Velocity { X = 7, Y = -3 });

		var withoutVelocity = create.CreateEntity();
		create.Set(withoutVelocity, new Position { X = 2, Y = 2 });
		world.Playback(create);

		var positionHandle = world.Compile<PositionQuerySpec>();
		Entity resolvedWithVelocity = default;
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

	[Fact]
	public void Run_WhenExecutingRepeatedHotPathAfterWarmup_ShouldNotAllocate()
	{
		using var world = new World(new WorldConfig()
		{
			EntityCapacity                = 512,
			ComponentTypeCapacity         = 16,
			CommandCapacity               = 2048,
			CommandPayloadCapacityPerType = 2048,
			QueryResultCapacity           = 512
		});

		using var commands = world.CreateCommandStream();
		for (var i = 0; i < 256; i++)
		{
			var entity = commands.CreateEntity();
			commands.Set(entity, new Position { X = i, Y = i });
			commands.Set(entity, new Velocity { X = 1, Y = -1 });
		}

		world.Playback(commands);
		var handle = world.Compile<PositionAndVelocityQuerySpec>();

		// Warm up JIT/caches so the measurement captures steady-state allocations.
		world.Run<PositionAndVelocityQuerySpec, IntegrateJob, Position, Velocity>(handle, new(0.5f));
		GC.Collect();
		GC.WaitForPendingFinalizers();
		GC.Collect();

		long before = GC.GetAllocatedBytesForCurrentThread();
		for (var i = 0; i < 200; i++)
			world.Run<PositionAndVelocityQuerySpec, IntegrateJob, Position, Velocity>(handle, new(0.5f));

		long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
		allocated.Should().Be(0);
	}

	[Fact]
	public void QueryCursor_ForEach_WhenExecutingRepeatedlyAfterWarmup_ShouldNotAllocate()
	{
		using var world = new World(new WorldConfig()
		{
			EntityCapacity                = 512,
			ComponentTypeCapacity         = 16,
			CommandCapacity               = 2048,
			CommandPayloadCapacityPerType = 2048,
			QueryResultCapacity           = 512
		});

		using var commands = world.CreateCommandStream();
		for (var i = 0; i < 256; i++)
		{
			var entity = commands.CreateEntity();
			commands.Set(entity, new Position { X = i, Y = i });
			commands.Set(entity, new Velocity { X = 1, Y = -1 });
		}

		world.Playback(commands);
		var handle = world.Compile<PositionAndVelocityQuerySpec>();
		QueryCursor.RefInAction<Position, Velocity> action = static (ref Position position, in Velocity velocity) =>
		{
			position.X += velocity.X;
			position.Y += velocity.Y;
		};

		// Warm up JIT/caches so the measurement captures steady-state allocations.
		using (var warmup = world.Execute(handle))
		{
			warmup.MoveNext();
			warmup.ForEach<Position, Velocity>(action);
		}

		GC.Collect();
		GC.WaitForPendingFinalizers();
		GC.Collect();

		for (var iteration = 0; iteration < 200; iteration++)
		{
			using var cursor = world.Execute(handle);
			cursor.MoveNext();
			cursor.ForEach<Position, Velocity>(action);
		}

		long before = GC.GetAllocatedBytesForCurrentThread();
		for (var iteration = 0; iteration < 200; iteration++)
		{
			using var cursor = world.Execute(handle);
			cursor.MoveNext();
			cursor.ForEach<Position, Velocity>(action);
		}

		long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
		allocated.Should().BeLessThanOrEqualTo(64);
	}

	[Fact]
	public void QueryCursor_Get_WhenExecutingSequentiallyAfterWarmup_ShouldNotAllocate()
	{
		const int entityCount = 256;
		using var world = new World(new WorldConfig()
		{
			EntityCapacity                = 512,
			ComponentTypeCapacity         = 16,
			CommandCapacity               = 2048,
			CommandPayloadCapacityPerType = 2048,
			QueryResultCapacity           = 512
		});

		using var commands = world.CreateCommandStream();
		for (var i = 0; i < entityCount; i++)
		{
			var entity = commands.CreateEntity();
			commands.Set(entity, new Position { X = i, Y = -i });
		}

		world.Playback(commands);
		var handle = world.Compile<PositionQuerySpec>();

		// Warm up JIT/caches so the measurement captures steady-state allocations.
		using (var warmup = world.Execute(handle))
		{
			warmup.MoveNext();
			for (var i = 0; i < entityCount; i++)
			{
				ref var position = ref warmup.Get<Position>(i);
				position.X += 1f;
			}
		}

		GC.Collect();
		GC.WaitForPendingFinalizers();
		GC.Collect();

		for (var iteration = 0; iteration < 200; iteration++)
		{
			using var cursor = world.Execute(handle);
			cursor.MoveNext();
			for (var i = 0; i < entityCount; i++)
			{
				ref var position = ref cursor.Get<Position>(i);
				position.X += 1f;
			}
		}

		long before = GC.GetAllocatedBytesForCurrentThread();
		for (var iteration = 0; iteration < 200; iteration++)
		{
			using var cursor = world.Execute(handle);
			cursor.MoveNext();
			for (var i = 0; i < entityCount; i++)
			{
				ref var position = ref cursor.Get<Position>(i);
				position.X += 1f;
			}
		}

		long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
		allocated.Should().BeLessThanOrEqualTo(64);
	}

	[Fact]
	public void ForEach_WhenExecutingRepeatedHotPathAfterWarmup_ShouldNotAllocate()
	{
		using var world = new World(new WorldConfig()
		{
			EntityCapacity                = 512,
			ComponentTypeCapacity         = 16,
			CommandCapacity               = 2048,
			CommandPayloadCapacityPerType = 2048,
			QueryResultCapacity           = 512
		});

		using var commands = world.CreateCommandStream();
		for (var i = 0; i < 256; i++)
		{
			var entity = commands.CreateEntity();
			commands.Set(entity, new Position { X = i, Y = i });
			commands.Set(entity, new Velocity { X = 1, Y = -1 });
		}

		world.Playback(commands);
		var handle = world.Compile<PositionAndVelocityQuerySpec>();
		QueryCursor.RefInAction<Position, Velocity> action = static (ref Position position, in Velocity velocity) =>
		{
			position.X += velocity.X;
			position.Y += velocity.Y;
		};

		// Warm up JIT/caches so the measurement captures steady-state allocations.
		world.ForEach(handle, action);

		GC.Collect();
		GC.WaitForPendingFinalizers();
		GC.Collect();

		for (var iteration = 0; iteration < 200; iteration++)
			world.ForEach(handle, action);

		long before = GC.GetAllocatedBytesForCurrentThread();
		for (var iteration = 0; iteration < 200; iteration++)
			world.ForEach(handle, action);

		long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
		allocated.Should().Be(0);
	}

	[Fact]
	public void Playback_WhenReusingCommandStreamForSetBurstAfterWarmup_ShouldNotAllocate()
	{
		using var world = new World(new WorldConfig()
		{
			EntityCapacity                = 512,
			ComponentTypeCapacity         = 16,
			CommandCapacity               = 4096,
			CommandPayloadCapacityPerType = 4096,
			QueryResultCapacity           = 512
		});

		using var create = world.CreateCommandStream();
		for (var i = 0; i < 256; i++)
		{
			var entity = create.CreateEntity();
			create.Set(entity, new Position { X = i, Y = i });
		}

		world.Playback(create);

		var handle = world.Compile<PositionQuerySpec>();
		var entities = new Entity[256];
		using (var cursor = world.Execute(handle))
		{
			cursor.MoveNext().Should().BeTrue();
			cursor.Current.CopyTo(entities);
		}

		using var updates = world.CreateCommandStream();
		for (var i = 0; i < entities.Length; i++)
			updates.Set(entities[i], new Position { X = i + 1, Y = -i });
		world.Playback(updates);

		GC.Collect();
		GC.WaitForPendingFinalizers();
		GC.Collect();

		long before = GC.GetAllocatedBytesForCurrentThread();
		for (var iteration = 0; iteration < 150; iteration++)
		{
			for (var i = 0; i < entities.Length; i++)
				updates.Set(entities[i], new Position { X = i + iteration, Y = -i - iteration });

			world.Playback(updates);
		}

		long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
		allocated.Should().Be(0);
	}

	[Fact]
	public void Playback_WhenRemoveCommandRecorded_ShouldRemoveComponentFromEntity()
	{
		using var world = new World(new WorldConfig()
		{
			EntityCapacity                = 16,
			ComponentTypeCapacity         = 16,
			CommandCapacity               = 32,
			CommandPayloadCapacityPerType = 32,
			QueryResultCapacity           = 16
		});

		using var createCommands = world.CreateCommandStream();
		var tempEntity = createCommands.CreateEntity();
		createCommands.Set(tempEntity, new Position { X = 1, Y = 1 });
		createCommands.Set(tempEntity, new Velocity { X = 2, Y = 2 });
		world.Playback(createCommands);

		var query = world.Compile<PositionAndVelocityQuerySpec>();
		Entity entity;
		using (var cursor = world.Execute(query))
		{
			cursor.MoveNext().Should().BeTrue();
			entity = cursor.Current[0];
		}

		using var removeCommands = world.CreateCommandStream();
		removeCommands.Remove<Velocity>(entity);
		world.Playback(removeCommands);

		world.Has<Velocity>(entity).Should().BeFalse();
		world.Has<Position>(entity).Should().BeTrue();
	}

	[Fact]
	public void Playback_WhenSetCommandsTargetSameEntity_ShouldApplyLastValue()
	{
		using var world = new World(new WorldConfig()
		{
			EntityCapacity                = 16,
			ComponentTypeCapacity         = 16,
			CommandCapacity               = 32,
			CommandPayloadCapacityPerType = 32,
			QueryResultCapacity           = 16
		});

		using var create = world.CreateCommandStream();
		var temporary = create.CreateEntity();
		create.Set(temporary, new Position { X = 1, Y = 1 });
		world.Playback(create);

		var handle = world.Compile<PositionQuerySpec>();
		Entity entity;
		using (var cursor = world.Execute(handle))
		{
			cursor.MoveNext().Should().BeTrue();
			entity = cursor.Current[0];
		}

		using var updates = world.CreateCommandStream();
		updates.Set(entity, new Position { X = 5, Y = 8 });
		updates.Set(entity, new Position { X = 9, Y = 3 });
		world.Playback(updates);

		var position = world.Get<Position>(entity);
		position.Should().Be(new Position { X = 9, Y = 3 });
	}

	[Fact]
	public void Playback_WhenRemoveCommandsTargetSameEntity_ShouldRemainWithoutComponent()
	{
		using var world = new World(new WorldConfig()
		{
			EntityCapacity                = 16,
			ComponentTypeCapacity         = 16,
			CommandCapacity               = 32,
			CommandPayloadCapacityPerType = 32,
			QueryResultCapacity           = 16
		});

		using var create = world.CreateCommandStream();
		var temporary = create.CreateEntity();
		create.Set(temporary, new Position { X = 1, Y = 1 });
		create.Set(temporary, new Velocity { X = 2, Y = 2 });
		world.Playback(create);

		var handle = world.Compile<PositionQuerySpec>();
		Entity entity;
		using (var cursor = world.Execute(handle))
		{
			cursor.MoveNext().Should().BeTrue();
			entity = cursor.Current[0];
		}

		using var remove = world.CreateCommandStream();
		remove.Remove<Velocity>(entity);
		remove.Remove<Velocity>(entity);
		world.Playback(remove);

		world.Has<Velocity>(entity).Should().BeFalse();
		world.Has<Position>(entity).Should().BeTrue();
	}

	[Fact]
	public void Playback_WhenDenseRemoveUsesCollidingEntityIds_ShouldRemoveMarkedComponentsOnly()
	{
		const int entityCount = 80;
		const int batchSize = 16;
		using var world = new World(new WorldConfig()
		{
			EntityCapacity                = 160,
			ComponentTypeCapacity         = 16,
			CommandCapacity               = 32,
			CommandPayloadCapacityPerType = 32,
			QueryResultCapacity           = 160
		});

		using var create = world.CreateCommandStream();
		for (var batchStart = 0; batchStart < entityCount; batchStart += batchSize)
		{
			for (var i = batchStart; i < batchStart + batchSize; i++)
			{
				var temporary = create.CreateEntity(new Position { X = i, Y = i });
				create.Set(temporary, new Velocity { X = 1, Y = -1 });
			}

			world.Playback(create);
		}

		var handle = world.Compile<PositionQuerySpec>();
		var entitiesByIndex = new Entity[entityCount];
		using (var cursor = world.Execute(handle))
		{
			cursor.MoveNext().Should().BeTrue();
			cursor.Current.Length.Should().Be(entityCount);
			for (var i = 0; i < cursor.Current.Length; i++)
			{
				var entity = cursor.Current[i];
				int index = (int)world.Get<Position>(entity).X;
				entitiesByIndex[index] = entity;
			}
		}

		using var remove = world.CreateCommandStream();
		for (var i = 0; i < 16; i++)
			remove.Remove<Velocity>(entitiesByIndex[i]);
		for (var i = 64; i < 80; i++)
			remove.Remove<Velocity>(entitiesByIndex[i]);
		world.Playback(remove);

		for (var i = 0; i < entityCount; i++)
		{
			bool shouldBeRemoved = i < 16 || i >= 64;
			world.Has<Velocity>(entitiesByIndex[i]).Should().Be(!shouldBeRemoved);
			world.Has<Position>(entitiesByIndex[i]).Should().BeTrue();
		}
	}

	[Fact]
	public void Playback_WhenDenseRemoveFullyMarksAChunk_ShouldKeepOtherChunkLocationsStable()
	{
		const int entityCount = 8;
		using var world = new World(new WorldConfig()
		{
			EntityCapacity                = 16,
			ComponentTypeCapacity         = 16,
			CommandCapacity               = 32,
			CommandPayloadCapacityPerType = 32,
			QueryResultCapacity           = 16,
			ChunkCapacity                 = 4
		});

		using var create = world.CreateCommandStream();
		for (var i = 0; i < entityCount; i++)
		{
			var temporary = create.CreateEntity(new Position { X = i, Y = i });
			create.Set(temporary, new Velocity { X = 1, Y = -1 });
		}

		world.Playback(create);

		var handle = world.Compile<PositionQuerySpec>();
		var entitiesByIndex = new Entity[entityCount];
		using (var cursor = world.Execute(handle))
		{
			cursor.MoveNext().Should().BeTrue();
			cursor.Current.Length.Should().Be(entityCount);
			for (var i = 0; i < cursor.Current.Length; i++)
			{
				var entity = cursor.Current[i];
				int index = (int)world.Get<Position>(entity).X;
				entitiesByIndex[index] = entity;
			}
		}

		using var removeFirstChunk = world.CreateCommandStream();
		for (var i = 0; i < 4; i++)
			removeFirstChunk.Remove<Velocity>(entitiesByIndex[i]);
		world.Playback(removeFirstChunk);

		for (var i = 0; i < entityCount; i++)
		{
			bool shouldBeRemoved = i < 4;
			world.Has<Velocity>(entitiesByIndex[i]).Should().Be(!shouldBeRemoved);
			world.Has<Position>(entitiesByIndex[i]).Should().BeTrue();
		}

		world.EntityCount.Should().Be(entityCount);
	}

	[Fact]
	public void Playback_WhenDenseRemovePartiallyMarksChunk_ShouldKeepSurvivorLocationsValid()
	{
		const int entityCount = 8;
		using var world = new World(new WorldConfig()
		{
			EntityCapacity                = 16,
			ComponentTypeCapacity         = 16,
			CommandCapacity               = 32,
			CommandPayloadCapacityPerType = 32,
			QueryResultCapacity           = 16,
			ChunkCapacity                 = 8
		});

		using var create = world.CreateCommandStream();
		for (var i = 0; i < entityCount; i++)
		{
			var temporary = create.CreateEntity(new Position { X = i, Y = i });
			create.Set(temporary, new Velocity { X = i, Y = -i });
		}

		world.Playback(create);

		var handle = world.Compile<PositionQuerySpec>();
		var entitiesByIndex = new Entity[entityCount];
		using (var cursor = world.Execute(handle))
		{
			cursor.MoveNext().Should().BeTrue();
			cursor.Current.Length.Should().Be(entityCount);
			for (var i = 0; i < cursor.Current.Length; i++)
			{
				var entity = cursor.Current[i];
				int index = (int)world.Get<Position>(entity).X;
				entitiesByIndex[index] = entity;
			}
		}

		using var remove = world.CreateCommandStream();
		remove.Remove<Velocity>(entitiesByIndex[0]);
		remove.Remove<Velocity>(entitiesByIndex[3]);
		remove.Remove<Velocity>(entitiesByIndex[7]);
		world.Playback(remove);

		for (var i = 0; i < entityCount; i++)
		{
			bool shouldBeRemoved = i is 0 or 3 or 7;
			world.Has<Velocity>(entitiesByIndex[i]).Should().Be(!shouldBeRemoved);
			world.Get<Position>(entitiesByIndex[i]).Should().Be(new Position { X = i, Y = i });
		}

		using var set = world.CreateCommandStream();
		set.Set(entitiesByIndex[1], new Velocity { X = 99, Y = 42 });
		world.Playback(set);
		world.Get<Velocity>(entitiesByIndex[1]).Should().Be(new Velocity { X = 99, Y = 42 });
	}

	[Fact]
	public void Playback_WhenDenseRemoveMovesEntireSourceArchetypeIntoPartiallyFilledTarget_ShouldKeepLocationsStable()
	{
		using var world = new World(new WorldConfig()
		{
			EntityCapacity                = 24,
			ComponentTypeCapacity         = 16,
			CommandCapacity               = 64,
			CommandPayloadCapacityPerType = 64,
			QueryResultCapacity           = 24,
			ChunkCapacity                 = 4
		});

		using var create = world.CreateCommandStream();
		var positionOnlyEntities = new Entity[2];
		for (var i = 0; i < positionOnlyEntities.Length; i++)
			positionOnlyEntities[i] = create.CreateEntity(new Position { X = 1_000 + i, Y = 1_000 + i });

		var positionVelocityEntities = new Entity[8];
		for (var i = 0; i < positionVelocityEntities.Length; i++)
		{
			var temporary = create.CreateEntity(new Position { X = i, Y = i });
			create.Set(temporary, new Velocity { X = i + 10, Y = -i });
			positionVelocityEntities[i] = temporary;
		}

		world.Playback(create);

		var allPositionHandle = world.Compile<PositionQuerySpec>();
		var entitiesByPositionX = new Entity[1_002];
		using (var cursor = world.Execute(allPositionHandle))
		{
			cursor.MoveNext().Should().BeTrue();
			cursor.Current.Length.Should().Be(10);
			for (var i = 0; i < cursor.Current.Length; i++)
			{
				var entity = cursor.Current[i];
				int key = (int)world.Get<Position>(entity).X;
				entitiesByPositionX[key] = entity;
			}
		}

		using var remove = world.CreateCommandStream();
		for (var i = 0; i < positionVelocityEntities.Length; i++)
			remove.Remove<Velocity>(entitiesByPositionX[i]);

		world.Playback(remove);

		for (var i = 0; i < 8; i++)
		{
			var entity = entitiesByPositionX[i];
			world.Get<Position>(entity).Should().Be(new Position { X = i, Y = i });
			world.Has<Velocity>(entity).Should().BeFalse();
		}

		for (var i = 0; i < positionOnlyEntities.Length; i++)
		{
			var entity = entitiesByPositionX[1_000 + i];
			world.Get<Position>(entity).Should().Be(new Position { X = 1_000 + i, Y = 1_000 + i });
			world.Has<Velocity>(entity).Should().BeFalse();
		}
	}

	[Fact]
	public void Playback_WhenSetRunContainsDifferentTransitions_ShouldApplyAllCommands()
	{
		using var world = new World(new WorldConfig()
		{
			EntityCapacity                = 16,
			ComponentTypeCapacity         = 16,
			CommandCapacity               = 64,
			CommandPayloadCapacityPerType = 64,
			QueryResultCapacity           = 16
		});

		using var create = world.CreateCommandStream();
		var firstTemporary = create.CreateEntity();
		create.Set(firstTemporary, new Position { X = 1, Y = 1 });

		var secondTemporary = create.CreateEntity();
		create.Set(secondTemporary, new Position { X = 2, Y = 2 });
		create.Set(secondTemporary, new Velocity { X = 3, Y = 4 });
		world.Playback(create);

		var positionOnly = world.Compile<PositionQuerySpec>();
		Entity first = default;
		Entity second = default;
		using (var cursor = world.Execute(positionOnly))
		{
			cursor.MoveNext().Should().BeTrue();
			for (var i = 0; i < cursor.Current.Length; i++)
			{
				var entity = cursor.Current[i];
				var position = world.Get<Position>(entity);
				if (position.X == 1)
					first = entity;
				else if (position.X == 2)
					second = entity;
			}
		}

		world.IsAlive(first).Should().BeTrue();
		world.IsAlive(second).Should().BeTrue();

		using var set = world.CreateCommandStream();
		set.Set(first, new Velocity { X = 10, Y = 20 });
		set.Set(second, new Velocity { X = 30, Y = 40 });
		world.Playback(set);

		world.Get<Velocity>(first).Should().Be(new Velocity { X = 10, Y = 20 });
		world.Get<Velocity>(second).Should().Be(new Velocity { X = 30, Y = 40 });
		world.Get<Position>(first).Should().Be(new Position { X = 1, Y = 1 });
		world.Get<Position>(second).Should().Be(new Position { X = 2, Y = 2 });
	}

	[Fact]
	public void Execute_WhenCompiledQueryUsesAny_ShouldMatchEntitiesWithAtLeastOneType()
	{
		using var world = new World(new WorldConfig()
		{
			EntityCapacity                = 32,
			ComponentTypeCapacity         = 16,
			CommandCapacity               = 64,
			CommandPayloadCapacityPerType = 64,
			QueryResultCapacity           = 32
		});

		using var commands = world.CreateCommandStream();
		var first = commands.CreateEntity();
		commands.Set(first, new Position { X = 1, Y = 0 });

		var second = commands.CreateEntity();
		commands.Set(second, new Velocity { X = 1, Y = 0 });

		var third = commands.CreateEntity();
		commands.Set(third, new Position { X = 1, Y = 0 });
		commands.Set(third, new Velocity { X = 1, Y = 0 });

		var fourth = commands.CreateEntity();
		commands.SetManaged(fourth, new ManagedTag { Payload = new Payload("x") });
		world.Playback(commands);

		var handle = world.Compile<AnyPositionOrVelocityQuerySpec>();
		using var cursor = world.Execute(handle);
		cursor.MoveNext().Should().BeTrue();
		cursor.Current.Length.Should().Be(3);
	}

	[Fact]
	public void Playback_WhenEntityCapacityExceeded_ShouldThrow()
	{
		using var world = new World(new WorldConfig()
		{
			EntityCapacity                = 1,
			ComponentTypeCapacity         = 8,
			CommandCapacity               = 8,
			CommandPayloadCapacityPerType = 8,
			QueryResultCapacity           = 1
		});

		using var commands = world.CreateCommandStream();
		commands.CreateEntity();
		commands.CreateEntity();

		var act = () => world.Playback(commands);
		act.Should().Throw<InvalidOperationException>()
		   .WithMessage("*Entity capacity*");
	}

	[Fact]
	public void CommandStream_WhenOverflowPolicyDropsNewest_ShouldTrackOverflowAndHighWatermark()
	{
		using var world = new World(new WorldConfig()
		{
			EntityCapacity                = 8,
			ComponentTypeCapacity         = 8,
			CommandCapacity               = 1,
			CommandPayloadCapacityPerType = 8,
			QueryResultCapacity           = 8,
			OverflowPolicy                = WorldOverflowPolicy.DropNewest
		});

		using var commands = world.CreateCommandStream();
		_ = commands.CreateEntity();
		commands.Destroy(new Entity(0, 0));

		var diagnostics = commands.GetDiagnostics();
		diagnostics.CommandCapacity.Should().Be(1);
		diagnostics.RecordedCommands.Should().Be(1);
		diagnostics.HighWatermark.Should().Be(1);
		diagnostics.OverflowCount.Should().Be(1);
	}

	[Fact]
	public void Playback_WhenManagedLaneComponentIsRecorded_ShouldBeReadableViaManagedApi()
	{
		using var world = new World(new WorldConfig()
		{
			EntityCapacity                = 8,
			ComponentTypeCapacity         = 8,
			CommandCapacity               = 16,
			CommandPayloadCapacityPerType = 16,
			QueryResultCapacity           = 8
		});

		using var commands = world.CreateCommandStream();
		var entity = commands.CreateEntity();
		var payload = new Payload("alpha");
		commands.SetManaged(entity, new ManagedTag { Payload = payload });
		world.Playback(commands);

		var positionQuery = world.Compile<ManagedTagQuerySpec>();
		using var cursor = world.Execute(positionQuery);
		cursor.MoveNext().Should().BeTrue();

		world.TryGetManaged(cursor.Current[0], out ManagedTag resolved).Should().BeTrue();
		resolved.Payload.Should().BeSameAs(payload);
	}

	[Fact]
	public void Playback_WhenReusingCommandStreamAcrossManagedBatches_ShouldApplyLatestBatch()
	{
		using var world = new World(new WorldConfig()
		{
			EntityCapacity                = 8,
			ComponentTypeCapacity         = 8,
			CommandCapacity               = 16,
			CommandPayloadCapacityPerType = 16,
			QueryResultCapacity           = 8
		});

		using var commands = world.CreateCommandStream();

		var firstPayload = new Payload("first");
		var firstEntity = commands.CreateEntity();
		commands.SetManaged(firstEntity, new ManagedTag { Payload = firstPayload });
		world.Playback(commands);

		var secondPayload = new Payload("second");
		var secondEntity = commands.CreateEntity();
		commands.SetManaged(secondEntity, new ManagedTag { Payload = secondPayload });
		world.Playback(commands);

		var query = world.Compile<ManagedTagQuerySpec>();
		using var cursor = world.Execute(query);
		cursor.MoveNext().Should().BeTrue();
		cursor.Current.Length.Should().Be(2);
		world.TryGetManaged(cursor.Current[0], out ManagedTag firstResolved).Should().BeTrue();
		world.TryGetManaged(cursor.Current[1], out ManagedTag secondResolved).Should().BeTrue();
		firstResolved.Payload.Should().BeSameAs(firstPayload);
		secondResolved.Payload.Should().BeSameAs(secondPayload);
	}

	[Fact]
	public void Reset_WhenCalled_ShouldInvalidatePreviousEntitiesAndClearComponentData()
	{
		using var world = new World(new WorldConfig()
		{
			EntityCapacity                = 8,
			ComponentTypeCapacity         = 8,
			CommandCapacity               = 16,
			CommandPayloadCapacityPerType = 16,
			QueryResultCapacity           = 8
		});

		using var commands = world.CreateCommandStream();
		var entity = commands.CreateEntity();
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
	public void Playback_WhenStreamBelongsToDifferentWorld_ShouldThrow()
	{
		using var worldA = new World(new WorldConfig()
		{
			EntityCapacity                = 8,
			ComponentTypeCapacity         = 8,
			CommandCapacity               = 8,
			CommandPayloadCapacityPerType = 8,
			QueryResultCapacity           = 8
		});
		using var worldB = new World(new WorldConfig()
		{
			EntityCapacity                = 8,
			ComponentTypeCapacity         = 8,
			CommandCapacity               = 8,
			CommandPayloadCapacityPerType = 8,
			QueryResultCapacity           = 8
		});

		using var foreign = worldA.CreateCommandStream();
		var act = () => worldB.Playback(foreign);

		act.Should().Throw<InvalidOperationException>()
		   .WithMessage("*different world*");
	}

	// ── 4a: Entity lifecycle ─────────────────────────────────────────────────

	[Fact]
	public void IsAlive_WhenEntityDespawned_ShouldReturnFalseForOldHandle()
	{
		using var world = new World(new WorldConfig
		{
			EntityCapacity                = 8,
			ComponentTypeCapacity         = 8,
			CommandCapacity               = 16,
			CommandPayloadCapacityPerType = 16,
			QueryResultCapacity           = 8
		});

		var entity = world.Spawn();
		world.IsAlive(entity).Should().BeTrue();

		world.Despawn(entity);
		world.IsAlive(entity).Should().BeFalse();
	}

	[Fact]
	public void Spawn_AfterDespawn_ShouldReuseSlotWithIncrementedVersion()
	{
		using var world = new World(new WorldConfig
		{
			EntityCapacity                = 8,
			ComponentTypeCapacity         = 8,
			CommandCapacity               = 16,
			CommandPayloadCapacityPerType = 16,
			QueryResultCapacity           = 8
		});

		var first = world.Spawn();
		world.Despawn(first);

		var second = world.Spawn();
		second.Id.Should().Be(first.Id);
		second.Version.Should().BeGreaterThan(first.Version);
		world.IsAlive(first).Should().BeFalse();
		world.IsAlive(second).Should().BeTrue();
	}

	[Fact]
	public void IsAlive_WhenEntityRespawnedMultipleTimes_ShouldAlwaysInvalidatePreviousHandle()
	{
		using var world = new World(new WorldConfig
		{
			EntityCapacity                = 8,
			ComponentTypeCapacity         = 8,
			CommandCapacity               = 16,
			CommandPayloadCapacityPerType = 16,
			QueryResultCapacity           = 8
		});

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

	// ── 4b: CommandStream overflow silent drops ───────────────────────────

	[Fact]
	public void Destroy_WhenCommandStreamFull_ShouldIncrementOverflowAndNotReplay()
	{
		using var world = new World(new WorldConfig
		{
			EntityCapacity                = 8,
			ComponentTypeCapacity         = 8,
			CommandCapacity               = 1,
			CommandPayloadCapacityPerType = 8,
			QueryResultCapacity           = 8,
			OverflowPolicy                = WorldOverflowPolicy.DropNewest
		});

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
	public void Remove_WhenCommandStreamFull_ShouldIncrementOverflowAndNotReplay()
	{
		using var world = new World(new WorldConfig
		{
			EntityCapacity                = 8,
			ComponentTypeCapacity         = 8,
			CommandCapacity               = 1,
			CommandPayloadCapacityPerType = 8,
			QueryResultCapacity           = 8,
			OverflowPolicy                = WorldOverflowPolicy.DropNewest
		});

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
		using var world = new World(new WorldConfig
		{
			EntityCapacity                = 8,
			ComponentTypeCapacity         = 8,
			CommandCapacity               = 1,
			CommandPayloadCapacityPerType = 8,
			QueryResultCapacity           = 8,
			OverflowPolicy                = WorldOverflowPolicy.DropNewest
		});

		using var commands = world.CreateCommandStream();
		commands.CreateEntity();

		var existing = new Entity(0, 0);
		commands.Set(existing, new Position { X = 1, Y = 2 });

		var diag = commands.GetDiagnostics();
		diag.RecordedCommands.Should().Be(1);
		diag.OverflowCount.Should().Be(1);
	}

	// ── 4c: CommandStream batch marker boundary (EntityCapacity = 32) ─────

	[Fact]
	public void Playback_WhenEntityCapacityIs32_ShouldUseSingleMarkerWordWithoutOverflow()
	{
		using var world = new World(new WorldConfig
		{
			EntityCapacity                = 32,
			ComponentTypeCapacity         = 8,
			CommandCapacity               = 64,
			CommandPayloadCapacityPerType = 64,
			QueryResultCapacity           = 32
		});

		// Spawn 32 entities with a component, then use a Set batch command to exercise marker bits
		var entities = new Entity[32];
		using var create = world.CreateCommandStream();
		for (var i = 0; i < 32; i++)
		{
			entities[i] = create.CreateEntity();
			create.Set(entities[i], new Position { X = i, Y = i });
		}

		world.Playback(create);

		// Resolve temporary entities via query
		var handle = world.Compile<PositionQuerySpec>();
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

	// ── 4e: 4-component ForEach ───────────────────────────────────────────

	[Fact]
	public void QueryCursor_ForEach4_WhenMutatingWithThreeReadonlyInputs_ShouldUpdateComponentsInPlace()
	{
		using var world = new World(new WorldConfig
		{
			EntityCapacity                = 16,
			ComponentTypeCapacity         = 16,
			CommandCapacity               = 64,
			CommandPayloadCapacityPerType = 64,
			QueryResultCapacity           = 16
		});

		using var commands = world.CreateCommandStream();
		for (var i = 0; i < 3; i++)
		{
			var entity = commands.CreateEntity();
			commands.Set(entity, new Position     { X = i,  Y = i  });
			commands.Set(entity, new Velocity     { X = 1,  Y = 2  });
			commands.Set(entity, new Acceleration { X = 10, Y = 20 });
			commands.Set(entity, new Scale        { Value = 3       });
		}

		world.Playback(commands);

		var handle = world.Compile<PositionVelocityAccelerationScaleQuerySpec>();
		using var cursor = world.Execute(handle);
		cursor.MoveNext().Should().BeTrue();
		cursor.ForEach<Position, Velocity, Acceleration, Scale>(
			(ref Position pos, in Velocity vel, in Acceleration acc, in Scale scale) =>
			{
				pos.X = vel.X + acc.X + scale.Value;
				pos.Y = vel.Y + acc.Y + scale.Value;
			}
		);

		for (var i = 0; i < cursor.Current.Length; i++)
		{
			var updated = cursor.Get<Position>(i);
			updated.X.Should().Be(14); // 1 + 10 + 3
			updated.Y.Should().Be(25); // 2 + 20 + 3
		}
	}

	private readonly struct PositionQuerySpec : ICompiledQuerySpec
	{
		public void Build(ref QueryBuilder builder) => builder.All<Position>();
	}

	private readonly struct PositionWithoutVelocityQuerySpec : ICompiledQuerySpec
	{
		public void Build(ref QueryBuilder builder)
		{
			builder.All<Position>();
			builder.None<Velocity>();
		}
	}

	private readonly struct PositionAndVelocityQuerySpec : ICompiledQuerySpec
	{
		public void Build(ref QueryBuilder builder)
		{
			builder.All<Position>();
			builder.All<Velocity>();
		}
	}

	private readonly struct PositionVelocityAccelerationQuerySpec : ICompiledQuerySpec
	{
		public void Build(ref QueryBuilder builder)
		{
			builder.All<Position>();
			builder.All<Velocity>();
			builder.All<Acceleration>();
		}
	}

	private readonly struct PositionVelocityAccelerationScaleQuerySpec : ICompiledQuerySpec
	{
		public void Build(ref QueryBuilder builder)
		{
			builder.All<Position>();
			builder.All<Velocity>();
			builder.All<Acceleration>();
			builder.All<Scale>();
		}
	}

	private readonly struct AnyPositionOrVelocityQuerySpec : ICompiledQuerySpec
	{
		public void Build(ref QueryBuilder builder)
		{
			builder.Any<Position>();
			builder.Any<Velocity>();
		}
	}

	private readonly struct ManagedTagQuerySpec : ICompiledQuerySpec
	{
		public void Build(ref QueryBuilder builder) => builder.All<ManagedTag>();
	}

	private struct Position
	{
		public float X;
		public float Y;
	}

	private struct Velocity
	{
		public float X;
		public float Y;
	}

	private struct Acceleration
	{
		public float X;
		public float Y;
	}

	private struct Scale
	{
		public float Value;
	}

	private sealed record Payload(string Name);

	private struct IntegrateJob(float dt) : IForEach<Position, Velocity>
	{
		public void Execute(ref Position component1, in Velocity component2)
		{
			component1.X += component2.X * dt;
			component1.Y += component2.Y * dt;
		}
	}

	private struct ManagedTag
	{
		public Payload? Payload;
	}
}

