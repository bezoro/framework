using System;
using System.Collections.Generic;
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
public class WorldAdvancedApiTests
{
	[Fact]
	public void RunParallel_WhenQueryMatches_ShouldProcessAllEntitiesExactlyOnce()
	{
		using var world = new World(new WorldConfig
		{
			EntityCapacity = 64,
			ComponentTypeCapacity = 16,
			CommandCapacity = 256,
			CommandPayloadCapacityPerType = 256,
			QueryResultCapacity = 64,
			MaxDegreeOfParallelism = 8
		});

		using var commands = world.CreateCommandStream();
		for (var i = 0; i < 32; i++)
		{
			var entity = commands.CreateEntity();
			commands.Set(entity, new Position { X = i, Y = i });
			commands.Set(entity, new Velocity { X = 2, Y = -3 });
		}

		world.Playback(commands);
		var handle = world.Compile<PositionVelocityQuerySpec>();

		world.RunParallel<PositionVelocityQuerySpec, IntegrateJob, Position, Velocity>(
			handle,
			new IntegrateJob(4f),
			degreeOfParallelism: 4
		);

		using var cursor = world.Execute(handle);
		cursor.MoveNext().Should().BeTrue();
		cursor.Current.Length.Should().Be(32);
		for (var i = 0; i < cursor.Current.Length; i++)
		{
			var position = cursor.Get<Position>(i);
			position.X.Should().Be(i + 8);
			position.Y.Should().Be(i - 12);
		}
	}

	[Fact]
	public void RunParallel_WhenDegreeOfParallelismIsInvalid_ShouldThrowArgumentOutOfRangeException()
	{
		using var world = new World();
		var handle = world.Compile<PositionQuerySpec>();

		var act = () => world.RunParallel<PositionQuerySpec, AdvanceJob, Position>(handle, new AdvanceJob(), 0);

		act.Should().Throw<ArgumentOutOfRangeException>()
		   .WithParameterName("degreeOfParallelism");
	}

	[Fact]
	public void CaptureAndRestoreSnapshot_WhenWorldHasRelationsResourcesAndComponents_ShouldRoundTrip()
	{
		using var source = new World(new WorldConfig
		{
			EntityCapacity = 64,
			ComponentTypeCapacity = 32,
			CommandCapacity = 256,
			CommandPayloadCapacityPerType = 256,
			QueryResultCapacity = 64
		});

		source.SetResource(new SimulationSettings { Gravity = 9.81f });
		source.SetResource(new SnapshotTagResource("alpha"));

		var target = source.Spawn(new Position { X = 10, Y = 20 });
		var follower = source.Spawn(new Position { X = 2, Y = 3 }, new Velocity { X = 1, Y = 0 });
		source.AddRelation<Follows>(follower, target);

		var writer = new InMemorySnapshotWriter();
		source.CaptureSnapshot(ref writer);

		using var restored = new World(new WorldConfig
		{
			EntityCapacity = 64,
			ComponentTypeCapacity = 32,
			CommandCapacity = 256,
			CommandPayloadCapacityPerType = 256,
			QueryResultCapacity = 64
		});

		var reader = new InMemorySnapshotReader(writer.Captured);
		restored.RestoreSnapshot(
			ref reader,
			new SnapshotDeserializationOptions
			{
				AllowedReferenceResourceTypes = [typeof(SnapshotTagResource)]
			}
		);

		restored.GetResource<SimulationSettings>().Gravity.Should().Be(9.81f);
		restored.GetResource<SnapshotTagResource>().Tag.Should().Be("alpha");

		var handle = restored.Compile<PositionQuerySpec>();
		var positions = new List<Position>(2);
		using (var cursor = restored.Execute(handle))
		{
			cursor.MoveNext().Should().BeTrue();
			cursor.Current.Length.Should().Be(2);
			for (var i = 0; i < cursor.Current.Length; i++)
				positions.Add(cursor.Get<Position>(i));
		}

		positions.Should().Contain(new Position { X = 10, Y = 20 });
		positions.Should().Contain(new Position { X = 2, Y = 3 });

		var relationHandle = restored.Compile<FollowerRelationQuerySpec>();
		using var relationCursor = restored.Execute(relationHandle);
		relationCursor.MoveNext().Should().BeTrue();
		relationCursor.Current.Length.Should().Be(1);
	}

	[Fact]
	public void RestoreSnapshot_WhenReferenceResourceIsNotAllowListed_ShouldThrowInvalidOperationException()
	{
		var snapshot = new WorldSnapshot(
			new[]
			{
				new SnapshotResourceRecord(typeof(SnapshotTagResource), new SnapshotTagResource("unsafe"))
			},
			Array.Empty<SnapshotEntityRecord>(),
			Array.Empty<SnapshotRelationRecord>()
		);

		using var world = new World();
		var reader = new InMemorySnapshotReader(snapshot);
		var act = () => world.RestoreSnapshot(ref reader);

		act.Should().Throw<InvalidOperationException>()
		   .WithMessage("*allow-listed*");
	}

	[Fact]
	public void GetQueryDiagnostics_WhenQueryIsCompiled_ShouldReturnFilterAndMatchCounts()
	{
		using var world = new World(new WorldConfig
		{
			EntityCapacity = 32,
			ComponentTypeCapacity = 16,
			CommandCapacity = 64,
			CommandPayloadCapacityPerType = 64,
			QueryResultCapacity = 32
		});

		using var commands = world.CreateCommandStream();
		var first = commands.CreateEntity();
		commands.Set(first, new Position { X = 1, Y = 1 });
		var second = commands.CreateEntity();
		commands.Set(second, new Position { X = 2, Y = 2 });
		commands.Set(second, new Velocity { X = 5, Y = 5 });
		var third = commands.CreateEntity();
		commands.Set(third, new Position { X = 3, Y = 3 });
		world.Playback(commands);

		var handle = world.Compile<PositionWithoutVelocityQuerySpec>();
		var diagnostics = world.GetQueryDiagnostics(handle);

		diagnostics.MatchingEntityCount.Should().Be(2);
		diagnostics.MatchingChunkCount.Should().BeGreaterThan(0);
		diagnostics.MatchingArchetypeCount.Should().BeGreaterThan(0);
		diagnostics.AllTypes.Should().ContainSingle(static type => type == typeof(Position));
		diagnostics.NoneTypes.Should().ContainSingle(static type => type == typeof(Velocity));
		diagnostics.RelatedRelationType.Should().BeNull();
	}

	[Fact]
	public void GetQueryDiagnostics_WhenUsingChangedFilter_ShouldNotAdvanceIncrementalWindow()
	{
		using var world = new World(new WorldConfig
		{
			EntityCapacity = 16,
			ComponentTypeCapacity = 16,
			CommandCapacity = 32,
			CommandPayloadCapacityPerType = 32,
			QueryResultCapacity = 16
		});

		using var commands = world.CreateCommandStream();
		var entity = commands.CreateEntity();
		commands.Set(entity, new Position { X = 8, Y = 9 });
		world.Playback(commands);

		var handle = world.Compile<ChangedPositionQuerySpec>();
		var diagnostics = world.GetQueryDiagnostics(handle);
		diagnostics.MatchingEntityCount.Should().Be(1);

		using var cursor = world.Execute(handle);
		cursor.MoveNext().Should().BeTrue();
		cursor.Current.Length.Should().Be(1);
	}

	private readonly struct PositionQuerySpec : ICompiledQuerySpec
	{
		public void Build(ref QueryBuilder builder) => builder.All<Position>();
	}

	private readonly struct PositionVelocityQuerySpec : ICompiledQuerySpec
	{
		public void Build(ref QueryBuilder builder)
		{
			builder.All<Position>();
			builder.All<Velocity>();
		}
	}

	private readonly struct PositionWithoutVelocityQuerySpec : ICompiledQuerySpec
	{
		public void Build(ref QueryBuilder builder)
		{
			builder.All<Position>();
			builder.None<Velocity>();
		}
	}

	private readonly struct FollowerRelationQuerySpec : ICompiledQuerySpec
	{
		public void Build(ref QueryBuilder builder)
		{
			builder.All<Position>();
			builder.Related<Follows>();
		}
	}

	private readonly struct ChangedPositionQuerySpec : ICompiledQuerySpec
	{
		public void Build(ref QueryBuilder builder) => builder.Changed<Position>();
	}

	private struct IntegrateJob(float dt) : IForEach<Position, Velocity>
	{
		public void Execute(ref Position component1, in Velocity component2)
		{
			component1.X += component2.X * dt;
			component1.Y += component2.Y * dt;
		}
	}

	private struct AdvanceJob : IForEach<Position>
	{
		public void Execute(ref Position component1) => component1.X += 1;
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

	private struct SimulationSettings
	{
		public float Gravity;
	}

	private sealed record SnapshotTagResource(string Tag);

	private struct Follows;

	private struct InMemorySnapshotWriter : IWorldSnapshotWriter
	{
		public WorldSnapshot Captured { get; private set; }

		public void Write(in WorldSnapshot snapshot) => Captured = snapshot;
	}

	private readonly struct InMemorySnapshotReader(WorldSnapshot snapshot) : IWorldSnapshotReader
	{
		public WorldSnapshot Read() => snapshot;
	}
}
