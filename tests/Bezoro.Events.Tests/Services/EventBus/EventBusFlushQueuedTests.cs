using System.Collections.Generic;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;
using Bezoro.Events.Tests.Services.Fixtures;

namespace Bezoro.Events.Tests.Services.EventBus;

[TestSubject(typeof(Events.Services.EventBus))]
public class EventBusFlushQueuedTests
{
	[Fact]
	public void FlushQueued_WhenCalled_ShouldClearQueue()
	{
		using var bus = new Events.Services.EventBus();
		bus.Enqueue(new TestEventA(1));
		bus.FlushQueued();
		bus.QueuedCount.Should().Be(0);
	}

	[Fact]
	public void FlushQueued_WhenEventsAreQueued_ShouldDispatchEnqueuedEvents()
	{
		using var bus      = new Events.Services.EventBus();
		var       received = new List<int>();
		bus.Subscribe<TestEventA>(ctx => received.Add(ctx.Data.Value));
		bus.Enqueue(new TestEventA(1));
		bus.Enqueue(new TestEventA(2));
		bus.FlushQueued();
		received.Should().ContainInOrder(1, 2);
	}

	[Fact]
	public void FlushQueued_WhenEventsAreQueued_ShouldDispatchInFifoOrder()
	{
		using var bus   = new Events.Services.EventBus();
		var       order = new List<string>();
		bus.Subscribe<TestEventA>(ctx => order.Add($"A{ctx.Data.Value}"));
		bus.Subscribe<TestEventB>(ctx => order.Add($"B{ctx.Data.Message}"));
		bus.Enqueue(new TestEventA(1));
		bus.Enqueue(new TestEventB("x"));
		bus.Enqueue(new TestEventA(2));
		bus.FlushQueued();
		order.Should().ContainInOrder("A1", "Bx", "A2");
	}

	[Fact]
	public void FlushQueued_WhenEventsAreQueued_ShouldReturnDispatchedCount()
	{
		using var bus = new Events.Services.EventBus();
		bus.Enqueue(new TestEventA(1));
		bus.Enqueue(new TestEventA(2));
		bus.Enqueue(new TestEventB("x"));
		bus.FlushQueued().Should().Be(3);
	}

	[Fact]
	public void FlushQueued_WhenEmpty_ShouldReturnZero()
	{
		using var bus = new Events.Services.EventBus();
		bus.FlushQueued().Should().Be(0);
	}
}
