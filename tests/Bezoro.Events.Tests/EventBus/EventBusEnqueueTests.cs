using Bezoro.Events.Services;
using Bezoro.Events.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Events.Tests.EventBus;

[TestSubject(typeof(Services.EventBus))]
public static class EventBusEnqueueTests
{
    public class Unit
    {
        [Fact]
        public void Enqueue_ShouldIncrementQueuedCount()
        {
            using var bus = new Services.EventBus();
            bus.Enqueue(new TestEventA(1));
            bus.QueuedCount.Should().Be(1);
        }

        [Fact]
        public void Enqueue_ShouldNotInvokeHandlersImmediately()
        {
            using var bus = new Services.EventBus();
            var called = false;
            bus.Subscribe<TestEventA>(_ => called = true);
            bus.Enqueue(new TestEventA(1));
            called.Should().BeFalse();
        }

        [Fact]
        public void Enqueue_MultipleTimes_ShouldTrackCount()
        {
            using var bus = new Services.EventBus();
            bus.Enqueue(new TestEventA(1));
            bus.Enqueue(new TestEventB("hello"));
            bus.Enqueue(new TestEventA(2));
            bus.QueuedCount.Should().Be(3);
        }
    }
}
