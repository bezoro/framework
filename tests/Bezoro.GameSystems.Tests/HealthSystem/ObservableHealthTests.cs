using Bezoro.Events.Services;
using Bezoro.GameSystems.HealthSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.GameSystems.Tests.HealthSystem;

[TestSubject(typeof(ObservableHealth))]
public class ObservableHealthTests
{
	[Fact]
	public void ShouldEnqueueEventWithBeforeAfterValues()
	{
		using var bus = new EventBus();
		var health     = new Health(100u, 50u);
		var observable = new ObservableHealth(health, bus);

		HealthChangedEvent received = default;
		var                called   = false;
		bus.Subscribe<HealthChangedEvent>(ctx =>
		{
			received = ctx.Data;
			called   = true;
		});

		observable.DecreaseCurrentHealthBy(10u);

		bus.QueuedCount.Should().Be(1);
		bus.FlushQueued();

		called.Should().BeTrue();
		received.Kind.Should().Be(HealthChangeKind.DecreaseCurrent);
		received.Value.Should().Be(10u);
		received.OldCurrent.Should().Be(50u);
		received.NewCurrent.Should().Be(40u);
		received.OldMax.Should().Be(100u);
		received.NewMax.Should().Be(100u);
		received.OldExcess.Should().Be(0u);
		received.NewExcess.Should().Be(0u);
		received.SupportsExcess.Should().BeTrue();
		received.Changed.Should().BeTrue();
	}

	[Fact]
	public void ShouldIncludeExcessChanges()
	{
		using var bus = new EventBus();
		var health     = new Health(100u, 100u);
		var observable = new ObservableHealth(health, bus);

		HealthChangedEvent received = default;
		bus.Subscribe<HealthChangedEvent>(ctx => received = ctx.Data);

		observable.IncreaseCurrentHealthBy(25u);
		bus.FlushQueued();

		received.Kind.Should().Be(HealthChangeKind.IncreaseCurrent);
		received.NewCurrent.Should().Be(100u);
		received.NewExcess.Should().Be(25u);
		received.DeltaExcess.Should().Be(25);
	}
}
