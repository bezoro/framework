using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Events.Tests.EventBus;

[TestSubject(typeof(Services.EventBus))]
public static class EventBusUnsubscribeTests
{
	public class Unit
	{
		[Fact]
		public void Unsubscribe_SameHandleTwice_ShouldReturnFalseSecondTime()
		{
			using var bus    = new Services.EventBus();
			var       handle = bus.Subscribe<TestEventA>(_ => { });
			bus.Unsubscribe(handle).Should().BeTrue();
			bus.Unsubscribe(handle).Should().BeFalse();
		}

		[Fact]
		public void Unsubscribe_ShouldDecrementSubscriptionCount()
		{
			using var bus    = new Services.EventBus();
			var       handle = bus.Subscribe<TestEventA>(_ => { });
			bus.Subscribe<TestEventA>(_ => { });
			bus.Unsubscribe(handle);
			bus.SubscriptionCount.Should().Be(1);
		}

		[Fact]
		public void Unsubscribe_ShouldPreventHandlerFromBeingCalled()
		{
			using var bus    = new Services.EventBus();
			var       called = false;
			var       handle = bus.Subscribe<TestEventA>(_ => called = true);
			bus.Unsubscribe(handle);
			bus.Publish(new TestEventA(1));
			called.Should().BeFalse();
		}

		[Fact]
		public void Unsubscribe_WithInvalidHandle_ShouldReturnFalse()
		{
			using var bus = new Services.EventBus();
			bus.Unsubscribe(new(999)).Should().BeFalse();
		}

		[Fact]
		public void Unsubscribe_WithValidHandle_ShouldReturnTrue()
		{
			using var bus    = new Services.EventBus();
			var       handle = bus.Subscribe<TestEventA>(_ => { });
			bus.Unsubscribe(handle).Should().BeTrue();
		}
	}
}
