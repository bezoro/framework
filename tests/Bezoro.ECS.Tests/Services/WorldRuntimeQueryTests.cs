using System;
using System.Collections.Generic;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;
using FluentAssertions;
using Xunit;

namespace Bezoro.ECS.Tests.Services;

public partial class WorldRuntimeTests
{
	[Fact]
	public void Compile_WhenDifferentSpecTypesAreCached_ShouldKeepDistinctQueryPlans()
	{
		using var world = new World(
			new WorldConfig
			{
				EntityCapacity                = 16,
				ComponentTypeCapacity         = 16,
				CommandCapacity               = 32,
				CommandPayloadCapacityPerType = 32,
				QueryResultCapacity           = 16
			}
		);

		using var commands = world.CreateCommandStream();
		var       entity   = commands.CreateEntity();
		commands.Set(entity, new Position { X = 1, Y = 1 });
		world.Playback(commands);

		var positionOnly        = world.Compile<PositionQuerySpec>();
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
	public void Execute_WhenCompiledQueryIsCachedBeforeMatchingArchetypeExists_ShouldRefreshAfterStructuralChange()
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

		var handle = world.Compile<PositionQuerySpec>();
		using (var before = world.Execute(handle))
		{
			before.MoveNext().Should().BeTrue();
			before.Current.Length.Should().Be(0);
		}

		using var commands = world.CreateCommandStream();
		var       created  = commands.CreateEntity();
		commands.Set(created, new Position { X = 42, Y = -7 });
		world.Playback(commands);

		using var after = world.Execute(handle);
		after.MoveNext().Should().BeTrue();
		after.Current.Length.Should().Be(1);
		world.Get<Position>(after.Current[0]).Should().Be(new Position { X = 42, Y = -7 });
	}


	[Fact]
	public void Execute_WhenCompiledQueryUsesAdded_ShouldTrackOnlyNewAdditionsAcrossExecutions()
	{
		using var world = new World(
			new WorldConfig
			{
				EntityCapacity                = 16,
				ComponentTypeCapacity         = 16,
				CommandCapacity               = 32,
				CommandPayloadCapacityPerType = 32,
				QueryResultCapacity           = 16
			}
		);

		var first  = world.Spawn(new Position { X = 1, Y = 1 });
		var second = world.Spawn(new Velocity { X = 5, Y = 5 });

		var handle = world.Compile<AddedPositionQuerySpec>();
		using (var initial = world.Execute(handle))
		{
			initial.MoveNext().Should().BeTrue();
			initial.Current.Length.Should().Be(1);
			initial.Current[0].Should().Be(first);
		}

		using (var unchanged = world.Execute(handle))
		{
			unchanged.MoveNext().Should().BeTrue();
			unchanged.Current.Length.Should().Be(0);
		}

		world.Set(first, new Position { X  = 11, Y = 12 });
		world.Add(second, new Position { X = 20, Y = 21 });
		using var added = world.Execute(handle);
		added.MoveNext().Should().BeTrue();
		added.Current.Length.Should().Be(1);
		added.Current[0].Should().Be(second);
	}

	[Fact]
	public void Execute_WhenCompiledQueryUsesAllAndNone_ShouldReturnExpectedEntities()
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
		var       first    = commands.CreateEntity();
		commands.Set(first, new Position { X = 1, Y = 1 });

		var second = commands.CreateEntity();
		commands.Set(second, new Position { X = 2, Y = 2 });
		commands.Set(second, new Velocity { X = 9, Y = 9 });
		world.Playback(commands);

		var       query  = world.Compile<PositionWithoutVelocityQuerySpec>();
		using var cursor = world.Execute(query);
		cursor.MoveNext().Should().BeTrue();
		cursor.Current.Length.Should().Be(1);
		cursor.Get<Position>(0).Should().Be(new Position { X = 1, Y = 1 });
	}


	[Fact]
	public void Execute_WhenCompiledQueryUsesAny_ShouldMatchEntitiesWithAtLeastOneType()
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

		using var commands = world.CreateCommandStream();
		var       first    = commands.CreateEntity();
		commands.Set(first, new Position { X = 1, Y = 0 });

		var second = commands.CreateEntity();
		commands.Set(second, new Velocity { X = 1, Y = 0 });

		var third = commands.CreateEntity();
		commands.Set(third, new Position { X = 1, Y = 0 });
		commands.Set(third, new Velocity { X = 1, Y = 0 });

		var fourth = commands.CreateEntity();
		commands.SetManaged(fourth, new ManagedTag { Payload = new("x") });
		world.Playback(commands);

		var       handle = world.Compile<AnyPositionOrVelocityQuerySpec>();
		using var cursor = world.Execute(handle);
		cursor.MoveNext().Should().BeTrue();
		cursor.Current.Length.Should().Be(3);
	}


	[Fact]
	public void Execute_WhenCompiledQueryUsesChanged_ShouldTrackChangesAcrossExecutions()
	{
		using var world = new World(
			new WorldConfig
			{
				EntityCapacity                = 16,
				ComponentTypeCapacity         = 16,
				CommandCapacity               = 32,
				CommandPayloadCapacityPerType = 32,
				QueryResultCapacity           = 16
			}
		);

		var first = world.Spawn(new Position { X = 1, Y = 1 });
		_ = world.Spawn(new Position { X = 2, Y = 2 });
		_ = world.Spawn(new Velocity { X = 7, Y = 7 });

		var handle = world.Compile<ChangedPositionQuerySpec>();
		using (var initial = world.Execute(handle))
		{
			initial.MoveNext().Should().BeTrue();
			initial.Current.Length.Should().Be(2);
		}

		using (var unchanged = world.Execute(handle))
		{
			unchanged.MoveNext().Should().BeTrue();
			unchanged.Current.Length.Should().Be(0);
		}

		world.Set(first, new Position { X = 11, Y = 12 });
		using var changed = world.Execute(handle);
		changed.MoveNext().Should().BeTrue();
		changed.Current.Length.Should().Be(1);
		changed.Current[0].Should().Be(first);
	}


	[Fact]
	public void Execute_WhenCompiledQueryUsesOptional_ShouldMatchEntitiesWithAndWithoutOptionalType()
	{
		using var world = new World(
			new WorldConfig
			{
				EntityCapacity                = 16,
				ComponentTypeCapacity         = 16,
				CommandCapacity               = 32,
				CommandPayloadCapacityPerType = 32,
				QueryResultCapacity           = 16
			}
		);

		var first  = world.Spawn(new Position { X = 1, Y = 1 });
		var second = world.Spawn(new Position { X = 2, Y = 2 }, new Velocity { X = 3, Y = 4 });
		_ = world.Spawn(new Velocity { X = 9, Y = 9 });

		var       handle = world.Compile<PositionWithOptionalVelocityQuerySpec>();
		using var cursor = world.Execute(handle);
		cursor.MoveNext().Should().BeTrue();
		cursor.Current.Length.Should().Be(2);
		var entities = cursor.Current.ToArray();
		entities.Should().Contain(first);
		entities.Should().Contain(second);
	}


	[Fact]
	public void Execute_WhenUsingMutableRefApis_ShouldTrackChangesForChangedFilter()
	{
		using var world = new World(
			new WorldConfig
			{
				EntityCapacity                = 16,
				ComponentTypeCapacity         = 32,
				CommandCapacity               = 32,
				CommandPayloadCapacityPerType = 32,
				QueryResultCapacity           = 16
			}
		);

		var entity         = world.Spawn(new Position { X = 1, Y = 1 });
		var changedHandle  = world.Compile<ChangedPositionQuerySpec>();
		var positionHandle = world.Compile<PositionQuerySpec>();

		using (var initial = world.Execute(changedHandle))
		{
			initial.MoveNext().Should().BeTrue();
			initial.Current.Length.Should().Be(1);
		}

		void AssertOnlyChangedEntity()
		{
			using var changed = world.Execute(changedHandle);
			changed.MoveNext().Should().BeTrue();
			changed.Current.Length.Should().Be(1);
			changed.Current[0].Should().Be(entity);
		}

		void ClearChangedWindow()
		{
			using var unchanged = world.Execute(changedHandle);
			unchanged.MoveNext().Should().BeTrue();
			unchanged.Current.Length.Should().Be(0);
		}

		ClearChangedWindow();
		ref var fromWorldGet = ref world.Get<Position>(entity);
		fromWorldGet.X += 1;
		AssertOnlyChangedEntity();

		ClearChangedWindow();
		var     accessor     = world.GetAccessor<Position>();
		ref var fromAccessor = ref accessor.Get(entity);
		fromAccessor.Y += 1;
		AssertOnlyChangedEntity();

		ClearChangedWindow();
		using (var cursor = world.Execute(positionHandle))
		{
			cursor.MoveNext().Should().BeTrue();
			cursor.Get<Position>(0).X += 1;
		}

		AssertOnlyChangedEntity();

		ClearChangedWindow();
		world.ForEach(positionHandle, (ref Position position) => position.X += 1);
		AssertOnlyChangedEntity();

		ClearChangedWindow();
		using (var cursor = world.Execute(positionHandle))
		{
			cursor.MoveNext().Should().BeTrue();
			cursor.ForEach((ref Position position) => position.Y += 1);
		}

		AssertOnlyChangedEntity();
	}


	[Fact]
	public void ForEach_WhenCursorIsActive_ShouldAllowIndependentDirectIteration()
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
		commands.Set(entity, new Position { X = 1, Y = 1 });
		commands.Set(entity, new Velocity { X = 1, Y = 1 });
		world.Playback(commands);

		var       handle = world.Compile<PositionAndVelocityQuerySpec>();
		using var cursor = world.Execute(handle);
		cursor.MoveNext().Should().BeTrue();

		world.ForEach(
			handle, (ref Position position, in Velocity velocity) => { position.X += velocity.X; }
		);

		cursor.Get<Position>(0).X.Should().Be(2);
	}

	[Fact]
	public void Execute_WhenAnotherCursorIsActive_ShouldAllowIndependentEnumeration()
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
		commands.Set(entity, new Position { X = 1, Y = 1 });
		world.Playback(commands);

		var handle = world.Compile<PositionQuerySpec>();
		using var first = world.Execute(handle);
		first.MoveNext().Should().BeTrue();

		using var second = world.Execute(handle);
		second.MoveNext().Should().BeTrue();

		first.Current.Length.Should().Be(1);
		second.Current.Length.Should().Be(1);
		var firstEntity  = first.Current[0];
		var secondEntity = second.Current[0];
		firstEntity.Id.Should().BeGreaterThanOrEqualTo(0);
		secondEntity.Should().Be(firstEntity);
	}


	[Fact]
	public void ForEach_WhenUsingCompiledHandleDirectly_ShouldMutateWithoutCursorMaterialization()
	{
		using var world = new World(
			new WorldConfig
			{
				EntityCapacity                = 16,
				ComponentTypeCapacity         = 16,
				CommandCapacity               = 48,
				CommandPayloadCapacityPerType = 48,
				QueryResultCapacity           = 16
			}
		);

		using var commands = world.CreateCommandStream();
		for (var i = 0; i < 3; i++)
		{
			var entity = commands.CreateEntity();
			commands.Set(entity, new Position { X = i, Y = i });
			commands.Set(entity, new Velocity { X = 5, Y = -1 });
		}

		world.Playback(commands);

		var handle = world.Compile<PositionAndVelocityQuerySpec>();
		world.ForEach(
			handle, (ref Position position, in Velocity velocity) =>
			{
				position.X += velocity.X;
				position.Y += velocity.Y;
			}
		);

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
	public void QueryCursor_ForEach_WhenMutatingSequentially_ShouldUpdateComponentsInPlace()
	{
		using var world = new World(
			new WorldConfig
			{
				EntityCapacity                = 16,
				ComponentTypeCapacity         = 16,
				CommandCapacity               = 32,
				CommandPayloadCapacityPerType = 32,
				QueryResultCapacity           = 16
			}
		);

		using var commands = world.CreateCommandStream();
		for (var i = 0; i < 3; i++)
		{
			var entity = commands.CreateEntity();
			commands.Set(entity, new Position { X = i, Y  = i });
			commands.Set(entity, new Velocity { X = 10, Y = -2 });
		}

		world.Playback(commands);

		var       handle = world.Compile<PositionAndVelocityQuerySpec>();
		using var cursor = world.Execute(handle);
		cursor.MoveNext().Should().BeTrue();
		cursor.ForEach((ref Position position, in Velocity velocity) =>
			{
				position.X += velocity.X;
				position.Y += velocity.Y;
			}
		);

		for (var i = 0; i < cursor.Current.Length; i++)
		{
			var updated = cursor.Get<Position>(i);
			updated.X.Should().Be(i + 10);
			updated.Y.Should().Be(i - 2);
		}
	}


	[Fact]
	public void QueryCursor_ForEach3_WhenMutatingWithTwoReadonlyInputs_ShouldUpdateComponentsInPlace()
	{
		using var world = new World(
			new WorldConfig
			{
				EntityCapacity                = 16,
				ComponentTypeCapacity         = 16,
				CommandCapacity               = 48,
				CommandPayloadCapacityPerType = 48,
				QueryResultCapacity           = 16
			}
		);

		using var commands = world.CreateCommandStream();
		for (var i = 0; i < 3; i++)
		{
			var entity = commands.CreateEntity();
			commands.Set(entity, new Position { X     = i, Y = i });
			commands.Set(entity, new Velocity { X     = 2, Y = 3 });
			commands.Set(entity, new Acceleration { X = 1, Y = -1 });
		}

		world.Playback(commands);

		var       handle = world.Compile<PositionVelocityAccelerationQuerySpec>();
		using var cursor = world.Execute(handle);
		cursor.MoveNext().Should().BeTrue();
		cursor.ForEach((ref Position position, in Velocity velocity, in Acceleration acceleration) =>
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


	// ── 4e: 4-component ForEach ───────────────────────────────────────────

	[Fact]
	public void QueryCursor_ForEach4_WhenMutatingWithThreeReadonlyInputs_ShouldUpdateComponentsInPlace()
	{
		using var world = new World(
			new WorldConfig
			{
				EntityCapacity                = 16,
				ComponentTypeCapacity         = 16,
				CommandCapacity               = 64,
				CommandPayloadCapacityPerType = 64,
				QueryResultCapacity           = 16
			}
		);

		using var commands = world.CreateCommandStream();
		for (var i = 0; i < 3; i++)
		{
			var entity = commands.CreateEntity();
			commands.Set(entity, new Position { X     = i, Y  = i });
			commands.Set(entity, new Velocity { X     = 1, Y  = 2 });
			commands.Set(entity, new Acceleration { X = 10, Y = 20 });
			commands.Set(entity, new Scale { Value    = 3 });
		}

		world.Playback(commands);

		var       handle = world.Compile<PositionVelocityAccelerationScaleQuerySpec>();
		using var cursor = world.Execute(handle);
		cursor.MoveNext().Should().BeTrue();
		cursor.ForEach((ref Position pos, in Velocity vel, in Acceleration acc, in Scale scale) =>
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


	[Fact]
	public void QueryCursor_Get_WhenResultSpansMultipleChunks_ShouldResolveEachEntityIndex()
	{
		using var world = new World(
			new WorldConfig
			{
				EntityCapacity                = 16,
				ComponentTypeCapacity         = 16,
				CommandCapacity               = 32,
				CommandPayloadCapacityPerType = 32,
				QueryResultCapacity           = 16,
				ChunkCapacity                 = 2
			}
		);

		using var commands = world.CreateCommandStream();
		for (var i = 0; i < 5; i++)
		{
			var entity = commands.CreateEntity();
			commands.Set(entity, new Position { X = i, Y = i });
		}

		world.Playback(commands);

		var       handle = world.Compile<PositionQuerySpec>();
		using var cursor = world.Execute(handle);
		cursor.MoveNext().Should().BeTrue();
		cursor.Current.Length.Should().Be(5);

		var seen = new bool[5];
		for (var i = 0; i < cursor.Current.Length; i++)
		{
			var position = cursor.Get<Position>(i);
			var index    = (int)position.X;
			index.Should().BeInRange(0, 4);
			seen[index] = true;
		}

		seen.Should().OnlyContain(static value => value);
	}


	[Fact]
	public void QueryCursor_Get_WhenSwitchingTypesAndTraversingBackward_ShouldResolveCorrectComponents()
	{
		using var world = new World(
			new WorldConfig
			{
				EntityCapacity                = 32,
				ComponentTypeCapacity         = 16,
				CommandCapacity               = 96,
				CommandPayloadCapacityPerType = 96,
				QueryResultCapacity           = 32,
				ChunkCapacity                 = 2
			}
		);

		using var commands = world.CreateCommandStream();
		for (var i = 0; i < 3; i++)
		{
			var entity = commands.CreateEntity();
			commands.Set(entity, new Position { X = 100 + i, Y = i });
			commands.Set(entity, new Velocity { X = 10 + i, Y  = -10 - i });
		}

		for (var i = 0; i < 3; i++)
		{
			var entity = commands.CreateEntity();
			commands.Set(entity, new Position { X     = 200 + i, Y = i });
			commands.Set(entity, new Velocity { X     = 20 + i, Y  = -20 - i });
			commands.Set(entity, new Acceleration { X = 1, Y       = 1 });
		}

		world.Playback(commands);

		var       handle = world.Compile<PositionAndVelocityQuerySpec>();
		using var cursor = world.Execute(handle);
		cursor.MoveNext().Should().BeTrue();
		cursor.Current.Length.Should().Be(6);

		int[] indexOrder = [0, 1, 2, 3, 2, 1, 4, 5, 4, 0];
		for (var i = 0; i < indexOrder.Length; i++)
		{
			int index  = indexOrder[i];
			var entity = cursor.Current[index];

			ref var velocity         = ref cursor.Get<Velocity>(index);
			var     expectedVelocity = world.Get<Velocity>(entity);
			velocity.X.Should().Be(expectedVelocity.X);
			velocity.Y.Should().Be(expectedVelocity.Y);

			ref var position         = ref cursor.Get<Position>(index);
			var     expectedPosition = world.Get<Position>(entity);
			position.X.Should().Be(expectedPosition.X);
			position.Y.Should().Be(expectedPosition.Y);
		}
	}


	[Fact]
	public void QueryCursor_Run_WhenUsingStructJob_ShouldMutateComponents()
	{
		using var world = new World(
			new WorldConfig
			{
				EntityCapacity                = 16,
				ComponentTypeCapacity         = 16,
				CommandCapacity               = 48,
				CommandPayloadCapacityPerType = 48,
				QueryResultCapacity           = 16
			}
		);

		using var commands = world.CreateCommandStream();
		for (var i = 0; i < 3; i++)
		{
			var entity = commands.CreateEntity();
			commands.Set(entity, new Position { X = i, Y = i });
			commands.Set(entity, new Velocity { X = 2, Y = -1 });
		}

		world.Playback(commands);

		var       handle = world.Compile<PositionAndVelocityQuerySpec>();
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
	public void QueryCursor_WhenUsingTraversalFamilies_ShouldPreserveOrderAndMutationAcrossAdapters()
	{
		static (List<int> order, float[] yValues) ExecuteWith(
			Action<World, QueryHandle<PositionAndVelocityQuerySpec>, List<int>> run)
		{
			using var world = new World(
				new WorldConfig
				{
					EntityCapacity                = 16,
					ComponentTypeCapacity         = 16,
					CommandCapacity               = 48,
					CommandPayloadCapacityPerType = 48,
					QueryResultCapacity           = 16
				}
			);

			using var commands = world.CreateCommandStream();
			for (var i = 0; i < 5; i++)
			{
				var entity = commands.CreateEntity();
				commands.Set(entity, new Position { X = i, Y = i * 10 });
				commands.Set(entity, new Velocity { X = 0, Y = 3 });
			}

			world.Playback(commands);

			var handle = world.Compile<PositionAndVelocityQuerySpec>();
			var order  = new List<int>();
			run(world, handle, order);

			using var cursor = world.Execute(handle);
			cursor.MoveNext().Should().BeTrue();
			var yValues = new float[cursor.Current.Length];
			for (var i = 0; i < cursor.Current.Length; i++)
				yValues[i] = cursor.Get<Position>(i).Y;

			return (order, yValues);
		}

		var foreachResult = ExecuteWith(
			static (world, handle, order) =>
			{
				using var cursor = world.Execute(handle);
				cursor.MoveNext().Should().BeTrue();
				cursor.ForEach((ref Position position, in Velocity velocity) =>
					{
						order.Add((int)position.X);
						position.Y += velocity.Y;
					}
				);
			}
		);

		var foreachEntityResult = ExecuteWith(
			static (world, handle, order) =>
			{
				using var cursor = world.Execute(handle);
				cursor.MoveNext().Should().BeTrue();
				cursor.ForEachEntity((Entity entity, ref Position position, in Velocity velocity) =>
					{
						order.Add((int)position.X);
						position.Y += velocity.Y;
					}
				);
			}
		);

		var runResult = ExecuteWith(
			static (world, handle, order) =>
			{
				using var cursor = world.Execute(handle);
				cursor.MoveNext().Should().BeTrue();
				cursor.Run<RecordingIntegrateJob, Position, Velocity>(new(order));
			}
		);

		var runEntityResult = ExecuteWith(
			static (world, handle, order) =>
			{
				using var cursor = world.Execute(handle);
				cursor.MoveNext().Should().BeTrue();
				cursor.RunEntity<RecordingEntityIntegrateJob, Position, Velocity>(new(order));
			}
		);

		int[] expectedOrder = [0, 1, 2, 3, 4];
		float[] expectedYValues = [3, 13, 23, 33, 43];

		foreachResult.order.Should().Equal(expectedOrder);
		foreachEntityResult.order.Should().Equal(expectedOrder);
		runResult.order.Should().Equal(expectedOrder);
		runEntityResult.order.Should().Equal(expectedOrder);

		foreachResult.yValues.Should().Equal(expectedYValues);
		foreachEntityResult.yValues.Should().Equal(expectedYValues);
		runResult.yValues.Should().Equal(expectedYValues);
		runEntityResult.yValues.Should().Equal(expectedYValues);
	}


	[Fact]
	public void Run_WhenUsingCompiledHandleAndStructJob_ShouldMutateComponents()
	{
		using var world = new World(
			new WorldConfig
			{
				EntityCapacity                = 16,
				ComponentTypeCapacity         = 16,
				CommandCapacity               = 48,
				CommandPayloadCapacityPerType = 48,
				QueryResultCapacity           = 16
			}
		);

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
}
