using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Options;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.ECS.Tests.Services;

[TestSubject(typeof(World))]
public class WorldApiContractTests
{
	[Fact]
	public void Add_WhenCalledWithoutValue_ShouldAddDefaultInitializedComponent()
	{
		var world  = new World();
		var entity = world.Spawn();

		world.Add<Health>(entity);

		world.Has<Health>(entity).Should().BeTrue();
		world.Get<Health>(entity).Should().Be(new Health { Current = 0, Max = 0 });
	}

	[Fact]
	public void Deserialize_WhenComponentLayoutHashDoesNotMatch_ShouldThrowInvalidOperationException()
	{
		var world  = new World();
		var entity = world.Spawn();
		world.Add(entity, new Position { X = 9f, Y = 4f });
		byte[] payload = world.Serialize();

		using (var stream = new MemoryStream(payload, false))
		{
			using (var reader = new BinaryReader(stream))
			{
				reader.ReadBytes(4); // magic
				reader.ReadInt32();  // version
				int archetypeCount = reader.ReadInt32();
				archetypeCount.Should().BeGreaterThan(0);
				int componentCount = reader.ReadInt32();
				componentCount.Should().BeGreaterThan(0);
				_ = reader.ReadString();

				var hashOffset = (int)stream.Position;
				payload[hashOffset] ^= 0xFF;
			}
		}

		var act = () => World.Deserialize(payload);

		act.Should().Throw<InvalidOperationException>()
		   .WithMessage("*layout mismatch*");
	}

	[Fact]
	public void Deserialize_WhenHeaderIsInvalid_ShouldThrowInvalidOperationException()
	{
		var payload = new byte[] { 1, 2, 3, 4, 5, 6 };

		var act = () => World.Deserialize(payload);

		act.Should().Throw<InvalidOperationException>()
		   .WithMessage("*snapshot*");
	}

	[Fact]
	public void Despawn_WhenChunkCompactsBoolComponent_ShouldPreserveRemainingEntityValue()
	{
		var world  = new World(new WorldOptions { ChunkCapacity = 2 });
		var first  = world.Spawn(new BoolFlag { IsSet           = false });
		var second = world.Spawn(new BoolFlag { IsSet           = true });

		world.Despawn(first);

		world.IsAlive(second).Should().BeTrue();
		world.Get<BoolFlag>(second).IsSet.Should().BeTrue();
	}

	[Fact]
	public void Diagnostics_WhenWorldContainsMultipleArchetypes_ShouldReportChunkAndMemoryTotals()
	{
		var world = new World(new WorldOptions { ChunkCapacity = 2 });
		world.Spawn(new Position { X = 1f, Y = 1f });
		world.Spawn(new Position { X = 2f, Y = 2f });
		world.Spawn(new Position { X = 3f, Y = 3f });
		world.Spawn(new Position { X = 4f, Y = 4f }, new Velocity { X = 5f, Y = 6f });

		var diagnostics = world.GetDiagnostics();

		diagnostics.EntityCount.Should().Be(4);
		diagnostics.ArchetypeCount.Should().Be(3);
		diagnostics.ChunkCount.Should().Be(3);
		diagnostics.Archetypes.Should().HaveCount(3);

		var empty = diagnostics.Archetypes.Single(x => x.ComponentTypes.Count == 0);
		empty.EntityCount.Should().Be(0);
		empty.ChunkCount.Should().Be(0);
		empty.AllocatedBytes.Should().Be(0);
		empty.LiveBytes.Should().Be(0);

		var positionOnly =
			diagnostics.Archetypes.Single(x => x.ComponentTypes.Count == 1 && x.ComponentTypes[0] == typeof(Position));

		positionOnly.EntityCount.Should().Be(3);
		positionOnly.ChunkCount.Should().Be(2);
		positionOnly.AllocatedEntitySlots.Should().Be(4);
		positionOnly.BytesPerEntity.Should().Be(16);
		positionOnly.AllocatedBytes.Should().Be(64);
		positionOnly.LiveBytes.Should().Be(48);

		var positionVelocity = diagnostics.Archetypes.Single(x =>
																 x.ComponentTypes.Count == 2 &&
																 x.ComponentTypes.Contains(typeof(Position)) &&
																 x.ComponentTypes.Contains(typeof(Velocity))
		);

		positionVelocity.EntityCount.Should().Be(1);
		positionVelocity.ChunkCount.Should().Be(1);
		positionVelocity.AllocatedEntitySlots.Should().Be(2);
		positionVelocity.BytesPerEntity.Should().Be(24);
		positionVelocity.AllocatedBytes.Should().Be(48);
		positionVelocity.LiveBytes.Should().Be(24);

		diagnostics.AllocatedBytes.Should().Be(positionOnly.AllocatedBytes + positionVelocity.AllocatedBytes);
		diagnostics.LiveBytes.Should().Be(positionOnly.LiveBytes + positionVelocity.LiveBytes);
		diagnostics.AllocatedBytes.Should().BeGreaterThanOrEqualTo(diagnostics.LiveBytes);
	}

	[Fact]
	public void Dispose_WhenSystemRegistered_ShouldInvokeOnDestroy()
	{
		var tracker = new LifecycleTracker();
		var world   = new World();
		world.AddSystem(new LifecycleSystem(tracker));

		tracker.Created.Should().Be(1);
		world.Dispose();
		tracker.Destroyed.Should().Be(1);
	}

	[Fact]
	public void ObserveAdd_WhenCreatingWithInitialComponentInPlayback_ShouldInvokeObserver()
	{
		var world = new World();
		var calls = 0;
		world.ObserveAdd((Entity _, ref Health health) =>
			{
				calls++;
				health.Current = health.Max;
			}
		);

		var commands = world.CreateCommandBuffer();
		commands.CreateEntity(new Health { Current = 1, Max = 8 });
		commands.Playback();

		int observedCurrent = -1;
		foreach (var chunk in world.Query<Health>())
		{
			chunk.Count.Should().Be(1);
			observedCurrent = chunk.ReadOnlyComponents<Health>()[0].Current;
		}

		calls.Should().Be(1);
		observedCurrent.Should().Be(8);
	}

	[Fact]
	public void ObserveAdd_WhenDirectMutationOccurs_ShouldNotInvokeObserver()
	{
		var world = new World();
		var calls = 0;
		world.ObserveAdd((Entity _, ref Health health) => calls++);

		var entity = world.Spawn();
		world.Add(entity, new Health { Current = 0, Max = 1 });
		world.Set(entity, new Health { Current = 1, Max = 1 });

		calls.Should().Be(0);
	}

	[Fact]
	public void ObserveAdd_WhenRegistered_ShouldAllowMutatingStoredComponentDuringPlayback()
	{
		var world = new World();
		world.ObserveAdd((Entity _, ref Health health) => health.Current = health.Max);

		var commands = world.CreateCommandBuffer();
		var entity   = commands.CreateEntity();
		commands.AddComponent(entity, new Health { Current = 0, Max = 10 });
		commands.Playback();

		int observedCurrent = -1;
		foreach (var chunk in world.Query<Health>())
		{
			chunk.Count.Should().Be(1);
			observedCurrent = chunk.ReadOnlyComponents<Health>()[0].Current;
		}

		observedCurrent.Should().Be(10);
	}

	[Fact]
	public void ObserveAdd_WhenSetAddsMissingComponentInPlayback_ShouldInvokeObserver()
	{
		var world = new World();
		var calls = 0;
		world.ObserveAdd((Entity _, ref Health health) =>
			{
				calls++;
				health.Current = health.Max;
			}
		);

		var entity   = world.Spawn();
		var commands = world.CreateCommandBuffer();
		commands.SetComponent(entity, new Health { Current = 1, Max = 6 });
		commands.Playback();

		calls.Should().Be(1);
		world.Get<Health>(entity).Current.Should().Be(6);
	}

	[Fact]
	public void ObserveAdd_WhenSettingExistingComponentInPlayback_ShouldInvokeObserver()
	{
		var world = new World();
		var calls = 0;
		world.ObserveAdd((Entity _, ref Health health) =>
			{
				calls++;
				health.Current = health.Max;
			}
		);

		var entity   = world.Spawn(new Health { Current = 1, Max = 4 });
		var commands = world.CreateCommandBuffer();
		commands.SetComponent(entity, new Health { Current = 2, Max = 9 });
		commands.Playback();

		calls.Should().Be(1);
		world.Get<Health>(entity).Current.Should().Be(9);
	}

	[Fact]
	public void ObserveAddAndRemove_WhenSubscriptionDisposed_ShouldStopReceivingPlaybackEvents()
	{
		var world              = new World();
		var addCalls           = 0;
		var removeCalls        = 0;
		var addSubscription    = world.ObserveAdd((Entity    _, ref Health health) => addCalls++);
		var removeSubscription = world.ObserveRemove((Entity _, in  Health health) => removeCalls++);

		addSubscription.Dispose();
		removeSubscription.Dispose();

		var entity   = world.Spawn();
		var commands = world.CreateCommandBuffer();
		commands.AddComponent(entity, new Health { Current = 1, Max = 5 });
		commands.RemoveComponent<Health>(entity);
		commands.Playback();

		addCalls.Should().Be(0);
		removeCalls.Should().Be(0);
	}

	[Fact]
	public void ObserveRemove_WhenDirectMutationOccurs_ShouldNotInvokeObserver()
	{
		var world = new World();
		var calls = 0;
		world.ObserveRemove((Entity _, in Velocity velocity) => calls++);

		var entity = world.Spawn(new Velocity { X = 2f, Y = 3f });
		world.Remove<Velocity>(entity);

		calls.Should().Be(0);
	}

	[Fact]
	public void ObserveRemove_WhenEntityIsDespawnedInPlayback_ShouldReceiveRemovedComponentValue()
	{
		var world    = new World();
		var removedX = 0f;
		var calls    = 0;
		world.ObserveRemove((Entity _, in Velocity velocity) =>
			{
				calls++;
				removedX = velocity.X;
			}
		);

		var entity   = world.Spawn(new Velocity { X = 11f, Y = 3f });
		var commands = world.CreateCommandBuffer();
		commands.DestroyEntity(entity);
		commands.Playback();

		calls.Should().Be(1);
		removedX.Should().Be(11f);
	}

	[Fact]
	public void ObserveRemove_WhenRegistered_ShouldInvokeExactlyOncePerPlaybackRemoval()
	{
		var world = new World();
		var calls = 0;
		world.ObserveRemove((Entity _, in Velocity velocity) => calls++);

		var entity   = world.Spawn(new Velocity { X = 2f, Y = 3f });
		var commands = world.CreateCommandBuffer();
		commands.RemoveComponent<Velocity>(entity);
		commands.Playback();

		calls.Should().Be(1);
	}

	[Fact]
	public void ObserveRemove_WhenRegistered_ShouldReceiveRemovedComponentValueDuringPlayback()
	{
		var world    = new World();
		var removedX = 0f;
		world.ObserveRemove((Entity _, in Velocity velocity) => removedX = velocity.X);

		var entity   = world.Spawn(new Velocity { X = 7f, Y = 1f });
		var commands = world.CreateCommandBuffer();
		commands.RemoveComponent<Velocity>(entity);
		commands.Playback();

		removedX.Should().Be(7f);
	}

	[Fact]
	public void QueryChanged_WhenChunkWritten_ShouldMatchChangedChunk()
	{
		var world  = new World();
		var entity = world.Spawn();
		world.Add(entity, new Position { X = 1f, Y = 2f });
		world.Tick(0f);

		var before = 0;
		foreach (var chunk in world.Query().All<Position>().Changed<Position>())
			before += chunk.Count;

		world.Query().All<Position>().ForEach(chunk =>
			{
				var positions = chunk.Components<Position>();
				for (var i = 0; i < chunk.Count; i++)
					positions[i].X += 1f;
			}
		);

		var after = 0;
		foreach (var chunk in world.Query().All<Position>().Changed<Position>())
			after += chunk.Count;

		before.Should().Be(0);
		after.Should().Be(1);
	}

	[Fact]
	public void QueryChanged_WhenComponentAddedThroughWorldApi_ShouldMatchChangedChunk()
	{
		var world  = new World();
		var entity = world.Spawn();
		world.Tick(0f);

		world.Add(entity, new Position { X = 5f, Y = 6f });

		var changed = 0;
		foreach (var chunk in world.Query().All<Position>().Changed<Position>())
			changed += chunk.Count;

		changed.Should().Be(1);
	}

	[Fact]
	public void QueryChanged_WhenComponentSetThroughWorldApi_ShouldMatchChangedChunk()
	{
		var world  = new World();
		var entity = world.Spawn();
		world.Add(entity, new Position { X = 1f, Y = 2f });
		world.Tick(0f);

		var before = 0;
		foreach (var chunk in world.Query().All<Position>().Changed<Position>())
			before += chunk.Count;

		world.Set(entity, new Position { X = 3f, Y = 4f });

		var after = 0;
		foreach (var chunk in world.Query().All<Position>().Changed<Position>())
			after += chunk.Count;

		before.Should().Be(0);
		after.Should().Be(1);
	}

	[Fact]
	public void QueryJob_WhenUsingIForEachOverload_ShouldApplyUpdates()
	{
		var world = new World();
		var entity = world.Spawn(
			new Position { X = 1f, Y = 2f },
			new Velocity { X = 3f, Y = 4f }
		);

		world.Query<Position, Velocity>()
			 .ForEach<MovementJob, Position, Velocity>(new() { DeltaTime = 0.5f });

		var position = world.Get<Position>(entity);
		position.X.Should().Be(2.5f);
		position.Y.Should().Be(4f);
	}

	[Fact]
	public void QueryOptional_WhenComponentMissing_ShouldExposeEmptySpan()
	{
		var world = new World();
		world.Spawn(new Position { X = 1, Y = 1 });
		world.Spawn(new Position { X = 2, Y = 2 }, new Velocity { X = 3, Y = 4 });

		var chunksWithOptional      = 0;
		var entitiesWithVelocity    = 0;
		var entitiesWithoutVelocity = 0;
		foreach (var chunk in world.Query().All<Position>().Optional<Velocity>())
		{
			var velocities = chunk.OptionalComponents<Velocity>();
			chunksWithOptional++;
			if (velocities.Length == 0)
				entitiesWithoutVelocity += chunk.Count;
			else
				entitiesWithVelocity += chunk.Count;
		}

		chunksWithOptional.Should().Be(2);
		entitiesWithVelocity.Should().Be(1);
		entitiesWithoutVelocity.Should().Be(1);
	}

	[Fact]
	public void QueryTyped_WhenUsingGenericWorldEntryPoint_ShouldMatchRequestedComponents()
	{
		var world = new World();
		world.Spawn(new Position { X = 1, Y = 1 }, new Velocity { X = 1, Y = 0 });
		world.Spawn(new Position { X = 2, Y = 2 });

		var count = 0;
		foreach (var chunk in world.Query<Position, Velocity>())
			count += chunk.Count;

		count.Should().Be(1);
	}

	[Fact]
	public void Relationships_WhenQueriedByTarget_ShouldMatchCorrectEntities()
	{
		var world   = new World();
		var parentA = world.Spawn();
		var parentB = world.Spawn();
		var childA  = world.Spawn();
		var childB  = world.Spawn();

		world.Add<ChildOf>(childA, parentA);
		world.Add<ChildOf>(childB, parentB);

		var relatedToA = 0;
		foreach (var chunk in world.Query().Related<ChildOf>(parentA))
			relatedToA += chunk.Count;

		var anyRelated = 0;
		foreach (var chunk in world.Query().Related<ChildOf>(Entity.Wildcard))
			anyRelated += chunk.Count;

		relatedToA.Should().Be(1);
		anyRelated.Should().Be(2);
	}

	[Fact]
	public void Relationships_WhenTargetIdIsReusedWithDifferentVersion_ShouldUseCorrectQueryCacheEntry()
	{
		var world          = new World();
		var originalParent = world.Spawn();
		var originalChild  = world.Spawn();
		world.Add<ChildOf>(originalChild, originalParent);

		var originalMatches = 0;
		foreach (var chunk in world.Query().Related<ChildOf>(originalParent))
			originalMatches += chunk.Count;

		world.Despawn(originalChild);
		world.Despawn(originalParent);

		var recycledParent = world.Spawn();
		recycledParent.Id.Should().Be(originalParent.Id);
		recycledParent.Version.Should().NotBe(originalParent.Version);

		var recycledChild = world.Spawn();
		world.Add<ChildOf>(recycledChild, recycledParent);

		var recycledMatches = 0;
		foreach (var chunk in world.Query().Related<ChildOf>(recycledParent))
			recycledMatches += chunk.Count;

		originalMatches.Should().Be(1);
		recycledMatches.Should().Be(1);
	}

	[Fact]
	public void Resources_WhenSet_ShouldBeReadableByReference()
	{
		var world = new World();
		world.SetResource(new GameTime { DeltaTime = 0.5f });

		ref var resource = ref world.GetResource<GameTime>();
		resource.DeltaTime = 1f;

		world.GetResource<GameTime>().DeltaTime.Should().Be(1f);
	}

	[Fact]
	public void Serialize_WhenCalled_ShouldProduceBinarySnapshotHeader()
	{
		var world  = new World();
		var entity = world.Spawn();
		world.Add(entity, new Position { X = 1f, Y = 2f });

		byte[] data = world.Serialize();

		data.Length.Should().BeGreaterThan(8);
		data[0].Should().Be((byte)'B');
		data[1].Should().Be((byte)'Z');
		data[2].Should().Be((byte)'E');
		data[3].Should().Be((byte)'C');
		data[0].Should().NotBe((byte)'{');
	}

	[Fact]
	public void Serialize_WhenRoundTripped_ShouldRestoreComponentsAndResources()
	{
		var world = new World();
		world.SetResource(new GameTime { DeltaTime = 0.25f });
		var entity = world.Spawn();
		world.Add(entity, new Position { X = 3f, Y = 4f });
		world.Add(entity, new Velocity { X = 2f, Y = 1f });

		byte[] data  = world.Serialize();
		var    clone = World.Deserialize(data);

		clone.EntityCount.Should().Be(1);
		clone.GetResource<GameTime>().DeltaTime.Should().Be(0.25f);

		var count = 0;
		foreach (var chunk in clone.Query().All<Position>().All<Velocity>())
		{
			count += chunk.Count;
			var position = chunk.Components<Position>()[0];
			position.X.Should().Be(3f);
			position.Y.Should().Be(4f);
		}

		count.Should().Be(1);
	}

	[Fact]
	public void Serialize_WhenRoundTripped_ShouldRestoreRelationships()
	{
		var world   = new World();
		var parentA = world.Spawn(new ParentTag { Id = 1 });
		var parentB = world.Spawn(new ParentTag { Id = 2 });
		var childA  = world.Spawn();
		var childB  = world.Spawn();

		world.Add<ChildOf>(childA, parentA);
		world.Add<ChildOf>(childB, parentB);

		byte[] data  = world.Serialize();
		var    clone = World.Deserialize(data);

		var cloneParentA = Entity.None;
		var cloneParentB = Entity.None;
		foreach (var chunk in clone.Query<ParentTag>())
		{
			var tags = chunk.ReadOnlyComponents<ParentTag>();
			for (var i = 0; i < chunk.Count; i++)
			{
				if (tags[i].Id == 1) cloneParentA = chunk.Entities[i];
				if (tags[i].Id == 2) cloneParentB = chunk.Entities[i];
			}
		}

		cloneParentA.Should().NotBe(Entity.None);
		cloneParentB.Should().NotBe(Entity.None);

		var relatedToA = 0;
		foreach (var chunk in clone.Query().Related<ChildOf>(cloneParentA))
			relatedToA += chunk.Count;

		var relatedToB = 0;
		foreach (var chunk in clone.Query().Related<ChildOf>(cloneParentB))
			relatedToB += chunk.Count;

		var anyRelated = 0;
		foreach (var chunk in clone.Query().Related<ChildOf>(Entity.Wildcard))
			anyRelated += chunk.Count;

		relatedToA.Should().Be(1);
		relatedToB.Should().Be(1);
		anyRelated.Should().Be(2);
	}

	[Fact]
	public void Spawn_WhenCalled_ShouldCreateAliveEntity()
	{
		var world  = new World();
		var entity = world.Spawn();

		world.IsAlive(entity).Should().BeTrue();
	}

	[Fact]
	public void Spawn_WhenGivenComponents_ShouldCreateEntityWithInferredArchetypeAndValues()
	{
		var world = new World();

		var entity = world.Spawn(
			new Position { X = 2f, Y   = 3f },
			new Velocity { X = 1.5f, Y = -0.25f }
		);

		world.IsAlive(entity).Should().BeTrue();
		world.Has<Position>(entity).Should().BeTrue();
		world.Has<Velocity>(entity).Should().BeTrue();
		world.Get<Position>(entity).Should().BeEquivalentTo(new Position { X = 2f, Y   = 3f });
		world.Get<Velocity>(entity).Should().BeEquivalentTo(new Velocity { X = 1.5f, Y = -0.25f });
	}

	[Fact]
	public void Systems_WhenAddedUsingGenericOverload_ShouldInstantiateAndRun()
	{
		var world = new World();
		GenericUpdateSystem.RunCount = 0;

		world.AddSystem<GenericUpdateSystem>();
		world.Tick(0.016f);

		GenericUpdateSystem.RunCount.Should().Be(1);
	}

	[Fact]
	public void Systems_WhenAddedWithStages_ShouldRunInStageOrder()
	{
		var world = new World();
		var order = new List<Stage>();

		world.AddSystem(new StageRecorder(order), Stage.Render);
		world.AddSystem(new StageRecorder(order), Stage.Input);
		world.AddSystem(new StageRecorder(order), Stage.PostTick);
		world.AddSystem(new StageRecorder(order), Stage.PreTick);
		world.AddSystem(new StageRecorder(order));

		world.Tick(0.016f);

		order.Should().Equal(Stage.Input, Stage.PreTick, Stage.Tick, Stage.PostTick, Stage.Render);
	}

	[Fact]
	public void Systems_WhenLoopPhaseIsFixedUpdate_ShouldRunOnlyDuringFixedUpdate()
	{
		var world  = new World();
		var system = new LoopPhaseRecorderSystem(SystemLoopPhase.FixedTick);
		world.AddSystem(system);

		world.Tick(1f / 60f);
		world.LateTick(1f / 60f);
		world.FixedTick(1f / 50f);

		system.UpdateCount.Should().Be(1);
		system.LastDeltaTime.Should().BeApproximately(1f / 50f, 0.0001f);
	}

	[Fact]
	public void Systems_WhenLoopPhaseIsLateUpdate_ShouldRunOnlyDuringLateUpdate()
	{
		var world  = new World();
		var system = new LoopPhaseRecorderSystem(SystemLoopPhase.LateTick);
		world.AddSystem(system);

		world.Tick(1f / 60f);
		world.FixedTick(1f / 50f);
		world.LateTick(1f / 60f);

		system.UpdateCount.Should().Be(1);
		system.LastDeltaTime.Should().BeApproximately(1f / 60f, 0.0001f);
	}

	[Fact]
	public void World_WhenCreatedWithName_ShouldExposeConfiguredName()
	{
		var world = new World("Main");

		world.Name.Should().Be("Main");
	}

	private struct BoolFlag : IComponent
	{
		public bool IsSet;
	}

	private readonly struct ChildOf;

	private struct GameTime
	{
		public float DeltaTime;
	}

	private sealed class GenericUpdateSystem : ISystem
	{
		public static int RunCount;

		public void Update(IWorld world, in SystemContext context) => RunCount++;
	}

	private struct Health : IComponent
	{
		public int Current;
		public int Max;
	}

	private sealed class LifecycleSystem(LifecycleTracker tracker) : ISystem
	{
		public void OnCreate(World  world) => tracker.Created++;
		public void OnDestroy(World world) => tracker.Destroyed++;

		public void Update(IWorld world, in SystemContext context) { }
	}

	private sealed class LifecycleTracker
	{
		public int Created;
		public int Destroyed;
	}

	private sealed class LoopPhaseRecorderSystem(SystemLoopPhase loopPhase) : ISystem
	{
		public SystemLoopPhase LoopPhase     { get; } = loopPhase;
		public float           LastDeltaTime { get; private set; }
		public int             UpdateCount   { get; private set; }

		public void Update(IWorld world, in SystemContext context)
		{
			UpdateCount++;
			LastDeltaTime = context.DeltaTime;
		}
	}

	private struct MovementJob : IForEach<Position, Velocity>
	{
		public float DeltaTime;

		public void Execute(ref Position position, in Velocity velocity)
		{
			position.X += velocity.X * DeltaTime;
			position.Y += velocity.Y * DeltaTime;
		}
	}

	private struct ParentTag : IComponent
	{
		public int Id;
	}

	private struct Position : IComponent
	{
		public float X;
		public float Y;
	}

	private sealed class StageRecorder(List<Stage> order) : ISystem
	{
		public void Update(IWorld world, in SystemContext context) => order.Add(context.Stage);
	}

	private struct Velocity : IComponent
	{
		public float X;
		public float Y;
	}
}
