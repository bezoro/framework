using Bezoro.Events.Tests.Services.Fixtures;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Events.Tests.Services.EventBus;

[TestSubject(typeof(Events.Services.EventBus))]
public class EventBusEnqueueTests
{
	[Fact]
	public void Enqueue_WhenCalled_ShouldIncrementQueuedCount()
	{
		using var bus = new Events.Services.EventBus();
		bus.Enqueue(new TestEventA(1));
		bus.QueuedCount.Should().Be(1);
	}

	[Fact]
	public void Enqueue_WhenCalled_ShouldNotInvokeHandlersImmediately()
	{
		using var bus    = new Events.Services.EventBus();
		var       called = false;
		bus.Subscribe<TestEventA>(_ => called = true);
		bus.Enqueue(new TestEventA(1));
		called.Should().BeFalse();
	}

	[Fact]
	public void Enqueue_WhenCalledMultipleTimes_ShouldTrackCount()
	{
		using var bus = new Events.Services.EventBus();
		bus.Enqueue(new TestEventA(1));
		bus.Enqueue(new TestEventB("hello"));
		bus.Enqueue(new TestEventA(2));
		bus.QueuedCount.Should().Be(3);
	}
}
