using System;
using Bezoro.Events.Tests.Services.Fixtures;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Events.Tests.Services.EventBus;

[TestSubject(typeof(Events.Services.EventBus))]
public class EventBusDisposeTests
{
	[Fact]
	public void Dispose_WhenCalled_ShouldClearSubscriptions()
	{
		var bus = new Events.Services.EventBus();
		bus.Subscribe<TestEventA>(_ => { });
		bus.Dispose();
		bus.SubscriptionCount.Should().Be(0);
	}

	[Fact]
	public void Dispose_WhenCalledMultipleTimes_ShouldNotThrow()
	{
		var bus = new Events.Services.EventBus();
		bus.Dispose();
		var act = () => bus.Dispose();
		act.Should().NotThrow();
	}

	[Fact]
	public void Enqueue_WhenCalledAfterDispose_ShouldThrowObjectDisposedException()
	{
		var bus = new Events.Services.EventBus();
		bus.Dispose();
		var act = () => bus.Enqueue(new TestEventA(1));
		act.Should().Throw<ObjectDisposedException>();
	}

	[Fact]
	public void FlushQueued_WhenCalledAfterDispose_ShouldThrowObjectDisposedException()
	{
		var bus = new Events.Services.EventBus();
		bus.Dispose();
		var act = () => bus.FlushQueued();
		act.Should().Throw<ObjectDisposedException>();
	}

	[Fact]
	public void Publish_WhenCalledAfterDispose_ShouldThrowObjectDisposedException()
	{
		var bus = new Events.Services.EventBus();
		bus.Dispose();
		var act = () => bus.Publish(new TestEventA(1));
		act.Should().Throw<ObjectDisposedException>();
	}

	[Fact]
	public void Subscribe_WhenCalledAfterDispose_ShouldThrowObjectDisposedException()
	{
		var bus = new Events.Services.EventBus();
		bus.Dispose();
		var act = () => bus.Subscribe<TestEventA>(_ => { });
		act.Should().Throw<ObjectDisposedException>();
	}

	[Fact]
	public void Unsubscribe_WhenCalledAfterDispose_ShouldThrowObjectDisposedException()
	{
		var bus    = new Events.Services.EventBus();
		var handle = bus.Subscribe<TestEventA>(_ => { });
		bus.Dispose();
		var act = () => bus.Unsubscribe(handle);
		act.Should().Throw<ObjectDisposedException>();
	}
}
