using System.Collections.Generic;
using Bezoro.Events.Services;
using Bezoro.Events.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Events.Tests.EventBus;

[TestSubject(typeof(Services.EventBus))]
public static class EventBusFlushQueuedTests
{
    public class Unit
    {
        [Fact]
        public void FlushQueued_ShouldDispatchEnqueuedEvents()
        {
            using var bus = new Services.EventBus();
            var received = new List<int>();
            bus.Subscribe<TestEventA>(ctx => received.Add(ctx.Data.Value));
            bus.Enqueue(new TestEventA(1));
            bus.Enqueue(new TestEventA(2));
            bus.FlushQueued();
            received.Should().ContainInOrder(1, 2);
        }

        [Fact]
        public void FlushQueued_ShouldReturnDispatchedCount()
        {
            using var bus = new Services.EventBus();
            bus.Enqueue(new TestEventA(1));
            bus.Enqueue(new TestEventA(2));
            bus.Enqueue(new TestEventB("x"));
            bus.FlushQueued().Should().Be(3);
        }

        [Fact]
        public void FlushQueued_WhenEmpty_ShouldReturnZero()
        {
            using var bus = new Services.EventBus();
            bus.FlushQueued().Should().Be(0);
        }

        [Fact]
        public void FlushQueued_ShouldClearQueue()
        {
            using var bus = new Services.EventBus();
            bus.Enqueue(new TestEventA(1));
            bus.FlushQueued();
            bus.QueuedCount.Should().Be(0);
        }

        [Fact]
        public void FlushQueued_ShouldDispatchInFifoOrder()
        {
            using var bus = new Services.EventBus();
            var order = new List<string>();
            bus.Subscribe<TestEventA>(ctx => order.Add($"A{ctx.Data.Value}"));
            bus.Subscribe<TestEventB>(ctx => order.Add($"B{ctx.Data.Message}"));
            bus.Enqueue(new TestEventA(1));
            bus.Enqueue(new TestEventB("x"));
            bus.Enqueue(new TestEventA(2));
            bus.FlushQueued();
            order.Should().ContainInOrder("A1", "Bx", "A2");
        }
    }
}
