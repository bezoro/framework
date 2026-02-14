using System;
using System.Collections.Generic;
using System.Numerics;
using Bezoro.ECS.Attributes;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;
using Bezoro.GameSystems.MovementSystem.Types;
using Bezoro.GameSystems.StreamingSystem.Services;
using Bezoro.GameSystems.StreamingSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;
using StreamingSystemType = Bezoro.GameSystems.StreamingSystem.Services.StreamingSystem;

namespace Bezoro.GameSystems.Tests.StreamingSystem;

[TestSubject(typeof(StreamingSystemType))]
public class StreamingSystemTests
{
	[Fact]
	public void Metadata_WhenInspectingStreamingSystem_ShouldDeclareWriteAccess()
	{
		// Arrange
		var systemType = typeof(StreamingSystemType);

		// Act / Assert
		systemType.IsDefined(typeof(ReadsAttribute<Position>), true).Should().BeTrue();
		systemType.IsDefined(typeof(WritesAttribute<StreamState>), true).Should().BeTrue();
	}

	[Fact]
	public void Tick_WhenEntityIsWithinStreamInDistance_ShouldMarkEntityStreamedInAndPublishEvent()
	{
		// Arrange
		var world  = new WorldV1();
		var system = new StreamingSystemType();
		world.AddSystem(system);
		world.SetResource(
			new StreamingConfig
			{
				ReferencePosition    = Vector3.Zero,
				StreamInDistance     = 10f,
				StreamOutDistance    = 15f,
				MaxEntitiesPerTick = 100
			}
		);

		var changedCount = 0;
		system.Changed += _ => changedCount++;

		var entity = world.Spawn(
			new Position { X = 5f, Y = 0f, Z = 0f },
			new StreamState()
		);

		// Act
		world.Tick(0f);

		// Assert
		var streamState = world.Get<StreamState>(entity);
		streamState.IsStreamedIn.Should().BeTrue();

		changedCount.Should().Be(1);
		var events = world.GetResource<StreamingEventsResource>();
		events.Count.Should().Be(1);
		events.TryDequeue(out var evt).Should().BeTrue();
		evt.TargetEntity.Should().Be(entity);
		evt.Transition.Should().Be(StreamingTransition.StreamedIn);
	}

	[Fact]
	public void Tick_WhenEntityStartsInHysteresisZoneAndIsNotStreamedIn_ShouldRemainStreamedOut()
	{
		// Arrange
		var world = new WorldV1();
		world.AddSystem(new StreamingSystemType());
		world.SetResource(
			new StreamingConfig
			{
				ReferencePosition    = Vector3.Zero,
				StreamInDistance     = 10f,
				StreamOutDistance    = 15f,
				MaxEntitiesPerTick = 100
			}
		);

		var entity = world.Spawn(
			new Position { X = 12f, Y = 0f, Z = 0f },
			new StreamState()
		);

		// Act
		world.Tick(0f);

		// Assert
		var streamState = world.Get<StreamState>(entity);
		streamState.IsStreamedIn.Should().BeFalse();

		var events = world.GetResource<StreamingEventsResource>();
		events.Count.Should().Be(0);
	}

	[Fact]
	public void Tick_WhenEntityMovesIntoHysteresisZoneAfterStreamingIn_ShouldRemainStreamedIn()
	{
		// Arrange
		var world = new WorldV1();
		world.AddSystem(new StreamingSystemType());
		world.SetResource(
			new StreamingConfig
			{
				ReferencePosition    = Vector3.Zero,
				StreamInDistance     = 10f,
				StreamOutDistance    = 15f,
				MaxEntitiesPerTick = 100
			}
		);

		var entity = world.Spawn(
			new Position { X = 5f, Y = 0f, Z = 0f },
			new StreamState()
		);
		world.Tick(0f);
		world.GetResource<StreamingEventsResource>().Clear();

		var moved = world.Get<Position>(entity);
		moved.X = 12f;
		moved.Y = 0f;
		moved.Z = 0f;
		world.Set(entity, in moved);

		// Act
		world.Tick(0f);

		// Assert
		var streamState = world.Get<StreamState>(entity);
		streamState.IsStreamedIn.Should().BeTrue();
		world.GetResource<StreamingEventsResource>().Count.Should().Be(0);
	}

	[Fact]
	public void Tick_WhenEntityMovesBeyondStreamOutDistance_ShouldMarkEntityStreamedOutAndPublishEvent()
	{
		// Arrange
		var world = new WorldV1();
		world.AddSystem(new StreamingSystemType());
		world.SetResource(
			new StreamingConfig
			{
				ReferencePosition    = Vector3.Zero,
				StreamInDistance     = 10f,
				StreamOutDistance    = 15f,
				MaxEntitiesPerTick = 100
			}
		);

		var entity = world.Spawn(
			new Position { X = 5f, Y = 0f, Z = 0f },
			new StreamState()
		);
		world.Tick(0f);
		world.GetResource<StreamingEventsResource>().Clear();

		var moved = world.Get<Position>(entity);
		moved.X = 20f;
		moved.Y = 0f;
		moved.Z = 0f;
		world.Set(entity, in moved);

		// Act
		world.Tick(0f);

		// Assert
		var streamState = world.Get<StreamState>(entity);
		streamState.IsStreamedIn.Should().BeFalse();

		var events = world.GetResource<StreamingEventsResource>();
		events.Count.Should().Be(1);
		events.TryDequeue(out var evt).Should().BeTrue();
		evt.TargetEntity.Should().Be(entity);
		evt.Transition.Should().Be(StreamingTransition.StreamedOut);
	}

	[Fact]
	public void Tick_WhenMaxEntitiesPerTickIsLimited_ShouldProcessEntitiesInRoundRobinOrder()
	{
		// Arrange
		var world = new WorldV1();
		world.AddSystem(new StreamingSystemType());
		world.SetResource(
			new StreamingConfig
			{
				ReferencePosition    = Vector3.Zero,
				StreamInDistance     = 10f,
				StreamOutDistance    = 15f,
				MaxEntitiesPerTick = 2
			}
		);

		var entities = new List<Entity>();
		for (var i = 0; i < 5; i++)
			entities.Add(
				world.Spawn(
					new Position { X = 5f + i, Y = 0f, Z = 0f },
					new StreamState()
				)
			);

		// Act
		world.Tick(0f);
		var firstTickStreamedInCount = CountStreamedIn(world, entities);

		world.Tick(0f);
		var secondTickStreamedInCount = CountStreamedIn(world, entities);

		world.Tick(0f);
		var thirdTickStreamedInCount = CountStreamedIn(world, entities);

		// Assert
		firstTickStreamedInCount.Should().Be(2);
		secondTickStreamedInCount.Should().Be(4);
		thirdTickStreamedInCount.Should().Be(5);
	}

	[Fact]
	public void Tick_WhenStreamOutDistanceIsLessThanStreamInDistance_ShouldThrow()
	{
		// Arrange
		var world = new WorldV1();
		world.AddSystem(new StreamingSystemType());
		world.SetResource(
			new StreamingConfig
			{
				ReferencePosition    = Vector3.Zero,
				StreamInDistance     = 10f,
				StreamOutDistance    = 9f,
				MaxEntitiesPerTick = 1
			}
		);

		// Act
		var act = () => world.Tick(0f);

		// Assert
		act.Should().Throw<ArgumentException>()
		   .WithMessage("*StreamOutDistance*StreamInDistance*");
	}

	[Fact]
	public void Tick_WhenNoEntitiesMatchQuery_ShouldNotThrow()
	{
		// Arrange
		var world = new WorldV1();
		world.AddSystem(new StreamingSystemType());

		// Act
		var act = () => world.Tick(0f);

		// Assert
		act.Should().NotThrow();
	}

	private static int CountStreamedIn(WorldV1 world, IReadOnlyList<Entity> entities)
	{
		var count = 0;
		for (var i = 0; i < entities.Count; i++)
		{
			if (world.Get<StreamState>(entities[i]).IsStreamedIn)
				count++;
		}

		return count;
	}
}
