using Bezoro.Events.Tests.Services.Fixtures;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Events.Tests.Services.EventBus;

[TestSubject(typeof(Events.Services.EventBus))]
public class EventBusUnsubscribeTests
{
	[Fact]
	public void Unsubscribe_WhenCalledTwiceWithSameHandle_ShouldReturnFalseSecondTime()
	{
		using var bus    = new Events.Services.EventBus();
		var       handle = bus.Subscribe<TestEventA>(_ => { });
		bus.Unsubscribe(handle).Should().BeTrue();
		bus.Unsubscribe(handle).Should().BeFalse();
	}

	[Fact]
	public void Unsubscribe_WhenCalledWithSubscribedHandle_ShouldDecrementSubscriptionCount()
	{
		using var bus    = new Events.Services.EventBus();
		var       handle = bus.Subscribe<TestEventA>(_ => { });
		bus.Subscribe<TestEventA>(_ => { });
		bus.Unsubscribe(handle);
		bus.SubscriptionCount.Should().Be(1);
	}

	[Fact]
	public void Unsubscribe_WhenCalledWithSubscribedHandle_ShouldPreventHandlerFromBeingCalled()
	{
		using var bus    = new Events.Services.EventBus();
		var       called = false;
		var       handle = bus.Subscribe<TestEventA>(_ => called = true);
		bus.Unsubscribe(handle);
		bus.Publish(new TestEventA(1));
		called.Should().BeFalse();
	}

	[Fact]
	public void Unsubscribe_WhenHandleIsInvalid_ShouldReturnFalse()
	{
		using var bus = new Events.Services.EventBus();
		bus.Unsubscribe(new(999)).Should().BeFalse();
	}

	[Fact]
	public void Unsubscribe_WhenHandleIsValid_ShouldReturnTrue()
	{
		using var bus    = new Events.Services.EventBus();
		var       handle = bus.Subscribe<TestEventA>(_ => { });
		bus.Unsubscribe(handle).Should().BeTrue();
	}
}
