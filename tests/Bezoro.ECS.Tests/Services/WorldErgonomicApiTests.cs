using System;
using System.Collections.Generic;
using Bezoro.ECS.Attributes;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.ECS.Tests.Services;

[TestSubject(typeof(World))]
public class WorldErgonomicApiTests
{
	[Fact]
	public void Query_WhenUsingGeneratedWithQuery_ShouldIterateEntitiesWithoutCursorCeremony()
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

		var first  = world.Spawn(new ErgonomicPosition { X = 1, Y = 2 });
		var second = world.Spawn(new ErgonomicPosition { X = 3, Y = 4 });
		_ = world.Spawn(new ErgonomicVelocity { X = 9, Y = 9 });

		var visited = new List<Entity>();

		world.Query<ErgonomicPositionQuery>().ForEach(entity => visited.Add(entity));

		visited.Should().Equal(first, second);
		world.Query<ErgonomicPositionQuery>().Count().Should().Be(2);
		world.Query<ErgonomicPositionQuery>().Any().Should().BeTrue();
	}

	[Fact]
	public void Query_WhenUsingTypedForEach_ShouldProvideEntityAndComponentsWithoutManualLookups()
	{
		using var world = new World();

		var entity = world.Spawn(
			new ErgonomicPosition { X = 1, Y = 2 },
			new ErgonomicVelocity { X = 3, Y = 4 }
		);

		Entity visitedEntity = default;
		world.Query<ErgonomicPositionVelocityQuery>().ForEach<ErgonomicPosition, ErgonomicVelocity>(
			(Entity entityId, ref ErgonomicPosition position, in ErgonomicVelocity velocity) =>
			{
				visitedEntity = entityId;
				position.X += velocity.X;
				position.Y += velocity.Y;
			}
		);

		visitedEntity.Should().Be(entity);
		world.Read<ErgonomicPosition>(entity).Should().Be(new ErgonomicPosition { X = 4, Y = 6 });
	}

	[Fact]
	public void Query_WhenUsingTypedForEachWhileCursorIsActive_ShouldAllowIndependentDirectIteration()
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

		var entity = world.Spawn(
			new ErgonomicPosition { X = 1, Y = 2 },
			new ErgonomicVelocity { X = 3, Y = 4 }
		);

		var handle = world.Compile<ErgonomicPositionVelocityQuery>();
		using var cursor = world.Execute(handle);
		cursor.MoveNext().Should().BeTrue();

		world.Query<ErgonomicPositionVelocityQuery>().ForEach<ErgonomicPosition, ErgonomicVelocity>(
			(Entity entityId, ref ErgonomicPosition position, in ErgonomicVelocity velocity) =>
			{
				entityId.Should().Be(entity);
				position.X += velocity.X;
				position.Y += velocity.Y;
			}
		);

		cursor.Get<ErgonomicPosition>(0).Should().Be(new ErgonomicPosition { X = 4, Y = 6 });
	}

	[Fact]
	public void Query_WhenUsingTypedReadOnlyForEachWithManagedComponent_ShouldProvideComponentWithoutManualLookups()
	{
		using var world = new World();

		var entity = world.Spawn(new ErgonomicManagedNote { Label = "pending", Count = 2 });

		Entity? observedEntity = null;
		string? observedLabel = null;
		world.Query<ErgonomicManagedNoteQuery>().ForEachRead<ErgonomicManagedNote>(
			(Entity entityId, in ErgonomicManagedNote note) =>
			{
				observedEntity = entityId;
				observedLabel  = note.Label;
			}
		);

		observedEntity.Should().Be(entity);
		observedLabel.Should().Be("pending");
	}

	[Fact]
	public void Query_WhenUsingTypedForEachWithManagedAndReadOnlyComponents_ShouldMutateWithoutManualLookups()
	{
		using var world = new World();

		var entity = world.Spawn(
			new ErgonomicManagedNote { Label = "pending", Count = 2 },
			new ErgonomicVelocity { X = 3, Y = 4 }
		);

		world.Query<ErgonomicManagedNoteVelocityQuery>().ForEach<ErgonomicManagedNote, ErgonomicVelocity>(
			(Entity entityId, ref ErgonomicManagedNote note, in ErgonomicVelocity velocity) =>
			{
				entityId.Should().Be(entity);
				note.Count += (int)(velocity.X + velocity.Y);
				note.Label += "!";
			}
		);

		world.Read<ErgonomicManagedNote>(entity).Should().Be(
			new ErgonomicManagedNote { Label = "pending!", Count = 9 }
		);
	}

	[Fact]
	public void Query_WhenCachedAndWorldDisposed_ShouldThrowOnUnmanagedFastPath()
	{
		var world = new World();
		world.Spawn(new ErgonomicPosition { X = 1, Y = 2 });
		var query = world.Query<ErgonomicPositionQuery>();

		world.Dispose();

		Action act = () => query.ForEach<ErgonomicPosition>(
			(Entity entityId, ref ErgonomicPosition position) => position.X += 1
		);

		act.Should().Throw<ObjectDisposedException>();
	}

	[Fact]
	public void ResourceApis_WhenUsingExplicitReadWriteMethods_ShouldAvoidExceptionDrivenFlow()
	{
		using var world = new World();

		world.HasResource<ErgonomicSettings>().Should().BeFalse();
		ErgonomicSettings existing;
		world.TryReadResource(out existing).Should().BeFalse();
		existing.Should().BeNull();

		ref var created = ref world.GetOrCreateResource<ErgonomicSettings>();
		created.Gravity = 9.81f;

		world.HasResource<ErgonomicSettings>().Should().BeTrue();
		world.TryReadResource(out existing).Should().BeTrue();
		existing.Should().NotBeNull();
		existing.Gravity.Should().Be(9.81f);
		world.ReadResource<ErgonomicSettings>().Gravity.Should().Be(9.81f);

		ref var writable = ref world.WriteResource<ErgonomicSettings>();
		writable.Gravity = 12.5f;

		world.ReadResource<ErgonomicSettings>().Gravity.Should().Be(12.5f);
		world.RemoveResource<ErgonomicSettings>().Should().BeTrue();
		world.HasResource<ErgonomicSettings>().Should().BeFalse();
	}

	[Fact]
	public void ComponentApis_WhenUsingTryWrite_ShouldMutateWithoutCopyWriteback()
	{
		using var world = new World();
		var       entity = world.Spawn(new ErgonomicPosition { X = 1, Y = 2 });

		world.TryWrite<ErgonomicPosition>(entity, out var position).Should().BeTrue();
		position.Value.X += 10;
		position.Value.Y += 20;

		world.Read<ErgonomicPosition>(entity).Should().Be(new ErgonomicPosition { X = 11, Y = 22 });
	}

}

[Query]
[With(typeof(ErgonomicPosition))]
internal readonly partial struct ErgonomicPositionQuery;

[Query]
[With(typeof(ErgonomicPosition))]
[With(typeof(ErgonomicVelocity))]
internal readonly partial struct ErgonomicPositionVelocityQuery;

[Query]
[With(typeof(ErgonomicManagedNote))]
internal readonly partial struct ErgonomicManagedNoteQuery;

[Query]
[With(typeof(ErgonomicManagedNote))]
[With(typeof(ErgonomicVelocity))]
internal readonly partial struct ErgonomicManagedNoteVelocityQuery;

internal struct ErgonomicManagedNote
{
	public string Label;
	public int    Count;
}

internal struct ErgonomicPosition
{
	public float X;
	public float Y;
}

internal sealed class ErgonomicSettings
{
	public float Gravity { get; set; }
}

internal struct ErgonomicVelocity
{
	public float X;
	public float Y;
}
