using System.Collections.Generic;
using Bezoro.ECS.Abstractions;
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
	public void Spawn_WhenCalled_ShouldCreateAliveEntity()
	{
		var world = new World();
		var entity = world.Spawn();

		world.IsAlive(entity).Should().BeTrue();
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
	public void Systems_WhenAddedWithStages_ShouldRunInStageOrder()
	{
		var world = new World();
		var order = new List<Stage>();

		world.AddSystem(new StageRecorder(order), Stage.Render);
		world.AddSystem(new StageRecorder(order), Stage.Input);
		world.AddSystem(new StageRecorder(order), Stage.PostUpdate);
		world.AddSystem(new StageRecorder(order), Stage.PreUpdate);
		world.AddSystem(new StageRecorder(order), Stage.Update);

		world.Update(0.016f);

		order.Should().Equal(Stage.Input, Stage.PreUpdate, Stage.Update, Stage.PostUpdate, Stage.Render);
	}

	[Fact]
	public void Relationships_WhenQueriedByTarget_ShouldMatchCorrectEntities()
	{
		var world = new World();
		var parentA = world.Spawn();
		var parentB = world.Spawn();
		var childA = world.Spawn();
		var childB = world.Spawn();

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
	public void QueryChanged_WhenChunkWritten_ShouldMatchChangedChunk()
	{
		var world = new World();
		var entity = world.Spawn();
		world.Add(entity, new Position { X = 1f, Y = 2f });
		world.Update(0f);

		var before = 0;
		foreach (var chunk in world.Query().All<Position>().Changed<Position>())
			before += chunk.Count;

		world.Query().All<Position>().ForEach(chunk =>
		{
			var positions = chunk.Components<Position>();
			for (var i = 0; i < chunk.Count; i++)
				positions[i].X += 1f;
		});

		var after = 0;
		foreach (var chunk in world.Query().All<Position>().Changed<Position>())
			after += chunk.Count;

		before.Should().Be(0);
		after.Should().Be(1);
	}

	[Fact]
	public void Serialize_WhenRoundTripped_ShouldRestoreComponentsAndResources()
	{
		var world = new World();
		world.SetResource(new GameTime { DeltaTime = 0.25f });
		var entity = world.Spawn();
		world.Add(entity, new Position { X = 3f, Y = 4f });
		world.Add(entity, new Velocity { X = 2f, Y = 1f });

		var data = world.Serialize();
		var clone = World.Deserialize(data);

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
	public void Dispose_WhenSystemRegistered_ShouldInvokeOnDestroy()
	{
		var tracker = new LifecycleTracker();
		var world = new World();
		world.AddSystem(new LifecycleSystem(tracker), Stage.Update);

		tracker.Created.Should().Be(1);
		world.Dispose();
		tracker.Destroyed.Should().Be(1);
	}

	private sealed class LifecycleSystem(LifecycleTracker tracker) : ISystem
	{
		public void OnCreate(World world) => tracker.Created++;
		public void OnDestroy(World world) => tracker.Destroyed++;
		public void Update(IWorld world, in SystemContext context)
		{
		}
	}

	private sealed class StageRecorder(List<Stage> order) : ISystem
	{
		public void Update(IWorld world, in SystemContext context) => order.Add(context.Stage);
	}

	private sealed class LifecycleTracker
	{
		public int Created;
		public int Destroyed;
	}

	private readonly struct ChildOf;

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

	private struct GameTime
	{
		public float DeltaTime;
	}
}
