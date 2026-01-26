using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.GameSystems.StreamingSystem.Abstractions;
using Bezoro.GameSystems.StreamingSystem.Services;
using Bezoro.GameSystems.StreamingSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.GameSystems.Tests.StreamingSystem;

[TestSubject(typeof(StreamingService))]
public static class StreamingServiceTests
{
	public class ConcurrencyTests
	{
		[Fact]
		public async Task WhenConcurrentRegistrationAndUnregistration_ShouldNotThrow()
		{
			using var system = new StreamingService();
			var       config = new StreamingConfig(() => Vector3.Zero);

			system.Start(config);

			var tasks = new List<Task>();

			for (var i = 0; i < 10; i++)
			{
				int index = i;
				tasks.Add(
					Task.Run(() =>
					{
						for (var j = 0; j < 50; j++)
						{
							var entity = new TestEntity(index * 1000 + j, new(j, 0, 0));
							system.Register(entity);
							Thread.Sleep(1);
							system.Unregister(entity);
						}
					}));
			}

			var act = async () => await Task.WhenAll(tasks);

			await act.Should().NotThrowAsync();
		}
	}

	public class IntegrationTests
	{
		[Fact]
		public async Task WhenEntityAlreadyStreamedIn_HysteresisZone_ShouldRemainStreamedIn()
		{
			using var system            = new StreamingService();
			var       referencePosition = Vector3.Zero;
			var       entity            = new TestEntity(1, new(5, 0, 0));

			var config = new StreamingConfig(
				() => referencePosition,
				10f,
				15f,
				10,
				100
			);

			system.Register(entity);
			system.Start(config);

			// Wait for entity to stream in
			await Task.Delay(100);
			entity.IsStreamedIn.Should().BeTrue();

			// Move to hysteresis zone (between 10 and 15)
			entity.StreamingPosition = new(12, 0, 0);

			// Wait for processing
			await Task.Delay(100);

			// Should remain streamed in (still within stream out distance)
			entity.IsStreamedIn.Should().BeTrue();
			entity.Events.Should().HaveCount(1);
			entity.Events[0].Should().Be("StreamIn");
		}

		[Fact]
		public async Task WhenEntityInHysteresisZone_ShouldNotFlicker()
		{
			using var system            = new StreamingService();
			var       referencePosition = Vector3.Zero;
			// Position between stream in (10) and stream out (15) distances
			var entity = new TestEntity(1, new(12, 0, 0));

			var config = new StreamingConfig(
				() => referencePosition,
				10f,
				15f,
				10,
				100
			);

			system.Register(entity);
			system.Start(config);

			// Wait for multiple processing cycles
			await Task.Delay(150);

			// Entity should not have streamed in (beyond stream in distance)
			entity.IsStreamedIn.Should().BeFalse();
			entity.Events.Should().BeEmpty();
		}

		[Fact]
		public async Task WhenEntityMovesBeyondStreamOutDistance_ShouldStreamOut()
		{
			using var system            = new StreamingService();
			var       referencePosition = Vector3.Zero;
			var       entity            = new TestEntity(1, new(5, 0, 0));

			var config = new StreamingConfig(
				() => referencePosition,
				10f,
				15f,
				10,
				100
			);

			system.Register(entity);
			system.Start(config);

			// Wait for entity to stream in
			await Task.Delay(100);
			entity.IsStreamedIn.Should().BeTrue();

			// Move entity beyond stream out distance
			entity.StreamingPosition = new(20, 0, 0);

			// Wait for processing
			await Task.Delay(100);

			entity.IsStreamedIn.Should().BeFalse();
			entity.Events.Should().Contain("StreamOut");
		}

		[Fact]
		public async Task WhenEntityMovesWithinStreamInDistance_ShouldStreamIn()
		{
			using var system            = new StreamingService();
			var       referencePosition = Vector3.Zero;
			var       entity            = new TestEntity(1, new(5, 0, 0));

			var config = new StreamingConfig(
				() => referencePosition,
				10f,
				15f,
				10,
				100
			);

			system.Register(entity);
			system.Start(config);

			// Wait for processing
			await Task.Delay(100);

			entity.IsStreamedIn.Should().BeTrue();
			entity.Events.Should().Contain("StreamIn");
		}

		[Fact]
		public async Task WhenManyEntitiesRegistered_ShouldProcessInRoundRobin()
		{
			using var system            = new StreamingService();
			var       referencePosition = Vector3.Zero;
			var       entities          = new List<TestEntity>();

			// Create 50 entities at various distances
			for (var i = 0; i < 50; i++)
				entities.Add(new(i, new(i * 0.2f, 0, 0)));

			var config = new StreamingConfig(
				() => referencePosition,
				10f,
				15f,
				10,
				10 // Process only 10 per frame
			);

			foreach (var entity in entities)
				system.Register(entity);

			system.Start(config);

			// Wait for multiple iterations to process all entities
			await Task.Delay(200);

			// Entities within stream in distance (0-10 units, roughly 50 entities at 0.2 per unit = 50 entities up to index 50)
			// At 0.2 distance per entity, entities 0-49 are at distances 0-9.8
			foreach (var entity in entities)
			{
				float distance = entity.StreamingPosition.X;
				if (distance <= 10f)
					entity.IsStreamedIn.Should().BeTrue($"Entity at distance {distance} should be streamed in");
			}
		}

		[Fact]
		public async Task WhenStoppingAndRestarting_ShouldWorkCorrectly()
		{
			using var system            = new StreamingService();
			var       referencePosition = Vector3.Zero;
			var       entity            = new TestEntity(1, new(5, 0, 0));

			var config = new StreamingConfig(
				() => referencePosition,
				10f,
				15f,
				10,
				100
			);

			system.Register(entity);

			// First run
			system.Start(config);
			await Task.Delay(100);
			system.Stop();

			entity.IsStreamedIn.Should().BeTrue();

			// Reset entity state for second test
			entity.StreamingPosition = new(20, 0, 0);

			// Second run
			system.Start(config);
			await Task.Delay(100);

			entity.IsStreamedIn.Should().BeFalse();
		}

		[Fact]
		public async Task WhenUnregisteringDuringProcessing_ShouldNotThrow()
		{
			using var system            = new StreamingService();
			var       referencePosition = Vector3.Zero;
			var       entities          = new List<TestEntity>();

			for (var i = 0; i < 100; i++)
				entities.Add(new(i, new(i * 0.1f, 0, 0)));

			var config = new StreamingConfig(
				() => referencePosition,
				100f,
				150f,
				5,
				10
			);

			foreach (var entity in entities)
				system.Register(entity);

			system.Start(config);

			// Unregister entities while processing
			var act = async () =>
			{
				foreach (var entity in entities)
				{
					system.Unregister(entity);
					await Task.Delay(1);
				}
			};

			await act.Should().NotThrowAsync();
		}

		[Fact]
		public void WhenEmptyEntityCollection_ShouldNotThrow()
		{
			using var system = new StreamingService();
			var config = new StreamingConfig(
				() => Vector3.Zero,
				10f,
				15f,
				10,
				100
			);

			var act = () =>
			{
				system.Start(config);
				Thread.Sleep(50);
				system.Stop();
			};

			act.Should().NotThrow();
		}
	}

	public class Register
	{
		[Fact]
		public void WhenDuplicateEntityId_ShouldNotDuplicate()
		{
			using var system  = new StreamingService();
			var       entity1 = new TestEntity(1, Vector3.Zero);
			var       entity2 = new TestEntity(1, Vector3.One);

			system.Register(entity1);
			system.Register(entity2);

			system.EntityCount.Should().Be(1);
		}

		[Fact]
		public void WhenEntityIsNull_ShouldThrow()
		{
			using var system = new StreamingService();

			var act = () => system.Register(null!);

			act.Should().Throw<ArgumentNullException>().WithParameterName("entity");
		}

		[Fact]
		public void WhenValidEntity_ShouldIncrementEntityCount()
		{
			using var system = new StreamingService();
			var       entity = new TestEntity(1, Vector3.Zero);

			system.Register(entity);

			system.EntityCount.Should().Be(1);
		}
	}

	public class Start
	{
		[Fact]
		public void WhenAlreadyRunning_ShouldBeNoOp()
		{
			using var system = new StreamingService();
			var       config = new StreamingConfig(() => Vector3.Zero);

			system.Start(config);
			var act = () => system.Start(config);

			act.Should().NotThrow();
			system.IsRunning.Should().BeTrue();
		}

		[Fact]
		public void WhenConfigHasNullReferencePosition_ShouldThrow()
		{
			using var system = new StreamingService();
			var       config = new StreamingConfig(null!);

			var act = () => system.Start(config);

			act.Should().Throw<ArgumentNullException>();
		}

		[Fact]
		public void WhenStreamOutDistanceLessThanStreamInDistance_ShouldThrow()
		{
			using var system = new StreamingService();
			var config = new StreamingConfig(
				() => Vector3.Zero,
				100f,
				50f // Invalid: less than stream in
			);

			var act = () => system.Start(config);

			act.Should().Throw<ArgumentException>()
			   .WithMessage("*StreamOutDistance*StreamInDistance*");
		}

		[Fact]
		public void WhenValidConfig_ShouldSetIsRunningTrue()
		{
			using var system = new StreamingService();
			var       config = new StreamingConfig(() => Vector3.Zero);

			system.Start(config);

			system.IsRunning.Should().BeTrue();
		}
	}

	public class Stop
	{
		[Fact]
		public void WhenNotRunning_ShouldBeNoOp()
		{
			using var system = new StreamingService();

			var act = () => system.Stop();

			act.Should().NotThrow();
		}

		[Fact]
		public void WhenRunning_ShouldSetIsRunningFalse()
		{
			using var system = new StreamingService();
			var       config = new StreamingConfig(() => Vector3.Zero);

			system.Start(config);
			system.Stop();

			system.IsRunning.Should().BeFalse();
		}
	}

	public class Unregister
	{
		[Fact]
		public void WhenEntityDoesNotExist_ShouldNotThrow()
		{
			using var system = new StreamingService();
			var       entity = new TestEntity(1, Vector3.Zero);

			var act = () => system.Unregister(entity);

			act.Should().NotThrow();
		}

		[Fact]
		public void WhenEntityExists_ShouldDecrementEntityCount()
		{
			using var system = new StreamingService();
			var       entity = new TestEntity(1, Vector3.Zero);
			system.Register(entity);

			system.Unregister(entity);

			system.EntityCount.Should().Be(0);
		}

		[Fact]
		public void WhenEntityIsNull_ShouldThrow()
		{
			using var system = new StreamingService();

			var act = () => system.Unregister(null!);

			act.Should().Throw<ArgumentNullException>().WithParameterName("entity");
		}
	}

	private sealed class TestEntity : IStreamableEntity
	{
		private readonly List<string> _events = new();

		public TestEntity(int id, Vector3 position)
		{
			EntityId          = id;
			StreamingPosition = position;
		}

		public int                   EntityId          { get; }
		public IReadOnlyList<string> Events            => _events;
		public bool                  IsStreamedIn      { get; private set; }
		public Vector3               StreamingPosition { get; set; }

		public void OnStreamIn()
		{
			IsStreamedIn = true;
			_events.Add("StreamIn");
		}

		public void OnStreamOut()
		{
			IsStreamedIn = false;
			_events.Add("StreamOut");
		}
	}
}
