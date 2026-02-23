using Bezoro.Core.Types;
using Bezoro.ECS.Attributes;
using Bezoro.ECS.Services;
using Bezoro.GameSystems.HealthSystem.Extensions;
using Bezoro.GameSystems.HealthSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;
using HealthSystemType = Bezoro.GameSystems.HealthSystem.Services.HealthSystem;

namespace Bezoro.GameSystems.Tests.HealthSystem;

[TestSubject(typeof(HealthSystemType))]
public class HealthSystemTests
{
	[Fact]
	public void Metadata_WhenInspectingHealthSystem_ShouldDeclareReadAndWriteAttributes()
	{
		// Arrange
		var systemType = typeof(HealthSystemType);

		// Act / Assert
		systemType.IsDefined(typeof(WritesAttribute<Health>),               true).Should().BeTrue();
		systemType.IsDefined(typeof(ReadsAttribute<HealthMutationRequest>), true).Should().BeTrue();
	}

	[Fact]
	public void QueueHealthDamage_WhenEntityDoesNotHaveHealth_ShouldReturnFalse()
	{
		// Arrange
		var world  = new World();
		var entity = world.Spawn();

		// Act
		bool queued = world.QueueHealthDamage(entity, 5u);

		// Assert
		queued.Should().BeFalse();
	}

	[Fact]
	public void Tick_WhenDamageRequestIsQueued_ShouldConsumeExcessBeforeCurrentAndPublishEvent()
	{
		// Arrange
		var world  = new World();
		var system = new HealthSystemType();
		world.AddSystem(system);

		var changedCount = 0;
		system.Changed += _ => changedCount++;

		var entity = world.Spawn(
			new Health(100u, 100u, 20u, 50u)
		);

		// Act
		world.QueueHealthDamage(entity, 25u).Should().BeTrue();
		world.Tick(0f);

		// Assert
		var health = world.Get<Health>(entity);
		health.Current.Should().Be(95u);
		health.ExcessCurrent.Should().Be(0u);

		changedCount.Should().Be(1);
		var events = world.GetResource<HealthEventsResource>();
		events.Count.Should().Be(1);
		events.TryDequeue(out var evt).Should().BeTrue();
		evt.TargetEntity.Should().Be(entity);
		evt.Kind.Should().Be(HealthChangeKind.Damage);
		evt.DeltaCurrent.Should().Be(-5);
		evt.DeltaExcess.Should().Be(-20);
	}

	[Fact]
	public void Tick_WhenDirectDamageRequestIsQueued_ShouldIgnoreExcess()
	{
		// Arrange
		var world = new World();
		world.AddSystem(new HealthSystemType());

		var entity = world.Spawn(
			new Health(100u, 40u, 20u, 50u)
		);

		// Act
		world.QueueHealthDirectDamage(entity, 30u).Should().BeTrue();
		world.Tick(0f);

		// Assert
		var health = world.Get<Health>(entity);
		health.Current.Should().Be(10u);
		health.ExcessCurrent.Should().Be(20u);
	}

	[Fact]
	public void Tick_WhenHealRequestIsQueued_ShouldFillCurrentWithoutOverflowIntoExcess()
	{
		// Arrange
		var world = new World();
		world.AddSystem(new HealthSystemType());

		var entity = world.Spawn(
			new Health(100u, 90u, 0u, 25u)
		);

		// Act
		world.QueueHealthHeal(entity, 20u).Should().BeTrue();
		world.Tick(0f);

		// Assert
		var health = world.Get<Health>(entity);
		health.Current.Should().Be(100u);
		health.ExcessCurrent.Should().Be(0u);
	}

	[Fact]
	public void Tick_WhenIncreaseHealthRequestIsQueued_ShouldOverflowIntoExcess()
	{
		// Arrange
		var world = new World();
		world.AddSystem(new HealthSystemType());

		var entity = world.Spawn(
			new Health(100u, 90u, 0u, 25u)
		);

		// Act
		world.QueueHealthIncreaseHealth(entity, 20u).Should().BeTrue();
		world.Tick(0f);

		// Assert
		var health = world.Get<Health>(entity);
		health.Current.Should().Be(100u);
		health.ExcessCurrent.Should().Be(10u);
	}

	[Fact]
	public void Tick_WhenMultipleRequestsAreQueued_ShouldApplyInQueueOrder()
	{
		// Arrange
		var world = new World();
		world.AddSystem(new HealthSystemType());
		var entity = world.Spawn(new Health(100u, 50u));

		// Act
		world.QueueHealthDamage(entity, 10u).Should().BeTrue();
		world.QueueHealthHeal(entity, 5u).Should().BeTrue();
		world.QueueHealthDamage(entity, 3u).Should().BeTrue();
		world.Tick(0f);

		// Assert
		var health = world.Get<Health>(entity);
		health.Current.Should().Be(42u);

		var events = world.GetResource<HealthEventsResource>();
		events.Count.Should().Be(3);
		events.TryDequeue(out var first).Should().BeTrue();
		events.TryDequeue(out var second).Should().BeTrue();
		events.TryDequeue(out var third).Should().BeTrue();

		first.Kind.Should().Be(HealthChangeKind.Damage);
		second.Kind.Should().Be(HealthChangeKind.Heal);
		third.Kind.Should().Be(HealthChangeKind.Damage);
	}

	[Fact]
	public void Tick_WhenSetMaxRequestedWithPreservePercentage_ShouldScaleCurrent()
	{
		// Arrange
		var world = new World();
		world.AddSystem(new HealthSystemType());
		var entity = world.Spawn(new Health(100u, 25u));

		// Act
		world.QueueSetHealthMax(entity, 200u, MaxValueUpdateMode.PreservePercentage).Should().BeTrue();
		world.Tick(0f);

		// Assert
		var health = world.Get<Health>(entity);
		health.Max.Should().Be(200u);
		health.Current.Should().Be(50u);
	}
}
