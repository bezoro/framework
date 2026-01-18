using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

using Bezoro.GameSystems.Streaming;

using FluentAssertions;

using JetBrains.Annotations;

using Xunit;

namespace Bezoro.GameSystems.Tests.Streaming;

[TestSubject(typeof(StreamingSystem))]
public static class StreamingSystemTests
{
	private sealed class TestEntity : IStreamableEntity
	{
		private readonly List<string> _events = new();

		public TestEntity(int id, Vector3 position)
		{
			EntityId = id;
			StreamingPosition = position;
		}

		public int EntityId { get; }
		public Vector3 StreamingPosition { get; set; }
		public bool IsStreamedIn { get; private set; }
		public IReadOnlyList<string> Events => _events;

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

	public class Register
	{
		[Fact]
		public void WhenEntityIsNull_ShouldThrow()
		{
			var act = () => StreamingSystem.Instance.Register(null!);

			act.Should().Throw<ArgumentNullException>().WithParameterName("entity");
		}

		[Fact]
		public void WhenValidEntity_ShouldIncrementEntityCount()
		{
			var system = StreamingSystem.Instance;
			var entity = new TestEntity(1000, Vector3.Zero);
			var initialCount = system.EntityCount;

			system.Register(entity);

			system.EntityCount.Should().Be(initialCount + 1);
			system.Unregister(entity);
		}

		[Fact]
		public void WhenDuplicateEntityId_ShouldNotDuplicate()
		{
			var system = StreamingSystem.Instance;
			var entity1 = new TestEntity(1001, Vector3.Zero);
			var entity2 = new TestEntity(1001, Vector3.One);
			var initialCount = system.EntityCount;

			system.Register(entity1);
			system.Register(entity2);

			system.EntityCount.Should().Be(initialCount + 1);
			system.Unregister(entity1);
		}
	}

	public class Unregister
	{
		[Fact]
		public void WhenEntityIsNull_ShouldThrow()
		{
			var act = () => StreamingSystem.Instance.Unregister(null!);

			act.Should().Throw<ArgumentNullException>().WithParameterName("entity");
		}

		[Fact]
		public void WhenEntityExists_ShouldDecrementEntityCount()
		{
			var system = StreamingSystem.Instance;
			var entity = new TestEntity(1002, Vector3.Zero);
			system.Register(entity);
			var countAfterRegister = system.EntityCount;

			system.Unregister(entity);

			system.EntityCount.Should().Be(countAfterRegister - 1);
		}

		[Fact]
		public void WhenEntityDoesNotExist_ShouldNotThrow()
		{
			var entity = new TestEntity(1003, Vector3.Zero);

			var act = () => StreamingSystem.Instance.Unregister(entity);

			act.Should().NotThrow();
		}
	}

	public class Start
	{
		[Fact]
		public void WhenConfigHasNullReferencePosition_ShouldThrow()
		{
			var config = new StreamingConfig
			{
				GetReferencePosition = null!,
				StreamInDistance = 10f,
				StreamOutDistance = 15f,
				MaxPerFrame = 100,
				FrameDelayMs = 16
			};

			var act = () => StreamingSystem.Instance.Start(config);

			act.Should().Throw<ArgumentNullException>();
			StreamingSystem.Instance.Stop();
		}

		[Fact]
		public void WhenValidConfig_ShouldSetIsRunningTrue()
		{
			var config = new StreamingConfig
			{
				GetReferencePosition = () => Vector3.Zero,
				StreamInDistance = 10f,
				StreamOutDistance = 15f,
				MaxPerFrame = 100,
				FrameDelayMs = 16
			};

			StreamingSystem.Instance.Start(config);

			StreamingSystem.Instance.IsRunning.Should().BeTrue();
			StreamingSystem.Instance.Stop();
		}

		[Fact]
		public void WhenAlreadyRunning_ShouldBeNoOp()
		{
			var config = new StreamingConfig
			{
				GetReferencePosition = () => Vector3.Zero,
				StreamInDistance = 10f,
				StreamOutDistance = 15f,
				MaxPerFrame = 100,
				FrameDelayMs = 16
			};

			StreamingSystem.Instance.Start(config);
			var act = () => StreamingSystem.Instance.Start(config);

			act.Should().NotThrow();
			StreamingSystem.Instance.IsRunning.Should().BeTrue();
			StreamingSystem.Instance.Stop();
		}
	}

	public class Stop
	{
		[Fact]
		public void WhenRunning_ShouldSetIsRunningFalse()
		{
			var config = new StreamingConfig
			{
				GetReferencePosition = () => Vector3.Zero,
				StreamInDistance = 10f,
				StreamOutDistance = 15f,
				MaxPerFrame = 100,
				FrameDelayMs = 16
			};

			StreamingSystem.Instance.Start(config);
			StreamingSystem.Instance.Stop();

			StreamingSystem.Instance.IsRunning.Should().BeFalse();
		}

		[Fact]
		public void WhenNotRunning_ShouldBeNoOp()
		{
			StreamingSystem.Instance.Stop();

			var act = () => StreamingSystem.Instance.Stop();

			act.Should().NotThrow();
		}
	}

	public class IntegrationTests
	{
		[Fact]
		public async Task WhenEntityMovesWithinStreamInDistance_ShouldStreamIn()
		{
			var referencePosition = Vector3.Zero;
			var entity = new TestEntity(2001, new Vector3(5, 0, 0));

			var config = new StreamingConfig
			{
				GetReferencePosition = () => referencePosition,
				StreamInDistance = 10f,
				StreamOutDistance = 15f,
				MaxPerFrame = 100,
				FrameDelayMs = 10
			};

			StreamingSystem.Instance.Register(entity);
			StreamingSystem.Instance.Start(config);

			// Wait for processing
			await Task.Delay(100);

			entity.IsStreamedIn.Should().BeTrue();
			entity.Events.Should().Contain("StreamIn");

			StreamingSystem.Instance.Stop();
			StreamingSystem.Instance.Unregister(entity);
		}

		[Fact]
		public async Task WhenEntityMovesBeyondStreamOutDistance_ShouldStreamOut()
		{
			var referencePosition = Vector3.Zero;
			var entity = new TestEntity(2002, new Vector3(5, 0, 0));

			var config = new StreamingConfig
			{
				GetReferencePosition = () => referencePosition,
				StreamInDistance = 10f,
				StreamOutDistance = 15f,
				MaxPerFrame = 100,
				FrameDelayMs = 10
			};

			StreamingSystem.Instance.Register(entity);
			StreamingSystem.Instance.Start(config);

			// Wait for entity to stream in
			await Task.Delay(100);
			entity.IsStreamedIn.Should().BeTrue();

			// Move entity beyond stream out distance
			entity.StreamingPosition = new Vector3(20, 0, 0);

			// Wait for processing
			await Task.Delay(100);

			entity.IsStreamedIn.Should().BeFalse();
			entity.Events.Should().Contain("StreamOut");

			StreamingSystem.Instance.Stop();
			StreamingSystem.Instance.Unregister(entity);
		}

		[Fact]
		public async Task WhenEntityInHysteresisZone_ShouldNotFlicker()
		{
			var referencePosition = Vector3.Zero;
			// Position between stream in (10) and stream out (15) distances
			var entity = new TestEntity(2003, new Vector3(12, 0, 0));

			var config = new StreamingConfig
			{
				GetReferencePosition = () => referencePosition,
				StreamInDistance = 10f,
				StreamOutDistance = 15f,
				MaxPerFrame = 100,
				FrameDelayMs = 10
			};

			StreamingSystem.Instance.Register(entity);
			StreamingSystem.Instance.Start(config);

			// Wait for multiple processing cycles
			await Task.Delay(150);

			// Entity should not have streamed in (beyond stream in distance)
			entity.IsStreamedIn.Should().BeFalse();
			entity.Events.Should().BeEmpty();

			StreamingSystem.Instance.Stop();
			StreamingSystem.Instance.Unregister(entity);
		}

		[Fact]
		public async Task WhenEntityAlreadyStreamedIn_HysteresisZone_ShouldRemainStreamedIn()
		{
			var referencePosition = Vector3.Zero;
			var entity = new TestEntity(2004, new Vector3(5, 0, 0));

			var config = new StreamingConfig
			{
				GetReferencePosition = () => referencePosition,
				StreamInDistance = 10f,
				StreamOutDistance = 15f,
				MaxPerFrame = 100,
				FrameDelayMs = 10
			};

			StreamingSystem.Instance.Register(entity);
			StreamingSystem.Instance.Start(config);

			// Wait for entity to stream in
			await Task.Delay(100);
			entity.IsStreamedIn.Should().BeTrue();

			// Move to hysteresis zone (between 10 and 15)
			entity.StreamingPosition = new Vector3(12, 0, 0);

			// Wait for processing
			await Task.Delay(100);

			// Should remain streamed in (still within stream out distance)
			entity.IsStreamedIn.Should().BeTrue();
			entity.Events.Should().HaveCount(1);
			entity.Events[0].Should().Be("StreamIn");

			StreamingSystem.Instance.Stop();
			StreamingSystem.Instance.Unregister(entity);
		}

		[Fact]
		public async Task WhenUnregisteringDuringProcessing_ShouldNotThrow()
		{
			var referencePosition = Vector3.Zero;
			var entities = new List<TestEntity>();

			for (var i = 0; i < 100; i++)
				entities.Add(new TestEntity(3000 + i, new Vector3(i * 0.1f, 0, 0)));

			var config = new StreamingConfig
			{
				GetReferencePosition = () => referencePosition,
				StreamInDistance = 100f,
				StreamOutDistance = 150f,
				MaxPerFrame = 10,
				FrameDelayMs = 5
			};

			foreach (var entity in entities)
				StreamingSystem.Instance.Register(entity);

			StreamingSystem.Instance.Start(config);

			// Unregister entities while processing
			var act = async () =>
			{
				foreach (var entity in entities)
				{
					StreamingSystem.Instance.Unregister(entity);
					await Task.Delay(1);
				}
			};

			await act.Should().NotThrowAsync();

			StreamingSystem.Instance.Stop();
		}

		[Fact]
		public async Task WhenStoppingAndRestarting_ShouldWorkCorrectly()
		{
			var referencePosition = Vector3.Zero;
			var entity = new TestEntity(4001, new Vector3(5, 0, 0));

			var config = new StreamingConfig
			{
				GetReferencePosition = () => referencePosition,
				StreamInDistance = 10f,
				StreamOutDistance = 15f,
				MaxPerFrame = 100,
				FrameDelayMs = 10
			};

			StreamingSystem.Instance.Register(entity);

			// First run
			StreamingSystem.Instance.Start(config);
			await Task.Delay(100);
			StreamingSystem.Instance.Stop();

			entity.IsStreamedIn.Should().BeTrue();

			// Reset entity state for second test
			entity.StreamingPosition = new Vector3(20, 0, 0);

			// Second run
			StreamingSystem.Instance.Start(config);
			await Task.Delay(100);

			entity.IsStreamedIn.Should().BeFalse();

			StreamingSystem.Instance.Stop();
			StreamingSystem.Instance.Unregister(entity);
		}

		[Fact]
		public async Task WhenManyEntitiesRegistered_ShouldProcessInRoundRobin()
		{
			var referencePosition = Vector3.Zero;
			var entities = new List<TestEntity>();

			// Create 50 entities at various distances
			for (var i = 0; i < 50; i++)
				entities.Add(new TestEntity(5000 + i, new Vector3(i * 0.2f, 0, 0)));

			var config = new StreamingConfig
			{
				GetReferencePosition = () => referencePosition,
				StreamInDistance = 10f,
				StreamOutDistance = 15f,
				MaxPerFrame = 10, // Process only 10 per frame
				FrameDelayMs = 10
			};

			foreach (var entity in entities)
				StreamingSystem.Instance.Register(entity);

			StreamingSystem.Instance.Start(config);

			// Wait for multiple iterations to process all entities
			await Task.Delay(200);

			// Entities within stream in distance (0-10 units, roughly 50 entities at 0.2 per unit = 50 entities up to index 50)
			// At 0.2 distance per entity, entities 0-49 are at distances 0-9.8
			foreach (var entity in entities)
			{
				var distance = entity.StreamingPosition.X;
				if (distance <= 10f)
					entity.IsStreamedIn.Should().BeTrue($"Entity at distance {distance} should be streamed in");
			}

			StreamingSystem.Instance.Stop();

			foreach (var entity in entities)
				StreamingSystem.Instance.Unregister(entity);
		}

		[Fact]
		public void WhenEmptyEntityCollection_ShouldNotThrow()
		{
			var config = new StreamingConfig
			{
				GetReferencePosition = () => Vector3.Zero,
				StreamInDistance = 10f,
				StreamOutDistance = 15f,
				MaxPerFrame = 100,
				FrameDelayMs = 10
			};

			var act = () =>
			{
				StreamingSystem.Instance.Start(config);
				Thread.Sleep(50);
				StreamingSystem.Instance.Stop();
			};

			act.Should().NotThrow();
		}
	}

	public class ConcurrencyTests
	{
		[Fact]
		public async Task WhenConcurrentRegistrationAndUnregistration_ShouldNotThrow()
		{
			var config = new StreamingConfig
			{
				GetReferencePosition = () => Vector3.Zero,
				StreamInDistance = 10f,
				StreamOutDistance = 15f,
				MaxPerFrame = 100,
				FrameDelayMs = 5
			};

			StreamingSystem.Instance.Start(config);

			var tasks = new List<Task>();
			var entitiesToCleanup = new List<TestEntity>();

			for (var i = 0; i < 10; i++)
			{
				var index = i;
				tasks.Add(Task.Run(() =>
				{
					for (var j = 0; j < 50; j++)
					{
						var entity = new TestEntity(6000 + index * 1000 + j, new Vector3(j, 0, 0));
						lock (entitiesToCleanup)
						{
							entitiesToCleanup.Add(entity);
						}
						StreamingSystem.Instance.Register(entity);
						Thread.Sleep(1);
						StreamingSystem.Instance.Unregister(entity);
					}
				}));
			}

			var act = async () => await Task.WhenAll(tasks);

			await act.Should().NotThrowAsync();

			StreamingSystem.Instance.Stop();
		}
	}
}
