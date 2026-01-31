using Bezoro.Events.Services;
using Bezoro.Events.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Events.Tests.EventBus;

[TestSubject(typeof(Services.EventBus))]
public static class EventBusPublishTests
{
    public class Unit
    {
        [Fact]
        public void Publish_ShouldInvokeHandler()
        {
            using var bus = new Services.EventBus();
            var received = 0;
            bus.Subscribe<TestEventA>(ctx => received = ctx.Data.Value);
            bus.Publish(new TestEventA(42));
            received.Should().Be(42);
        }

        [Fact]
        public void Publish_ShouldInvokeAllHandlers()
        {
            using var bus = new Services.EventBus();
            var count = 0;
            bus.Subscribe<TestEventA>(_ => count++);
            bus.Subscribe<TestEventA>(_ => count++);
            bus.Publish(new TestEventA(1));
            count.Should().Be(2);
        }

        [Fact]
        public void Publish_WithNoSubscribers_ShouldReturnContextWithHandledFalse()
        {
            using var bus = new Services.EventBus();
            var ctx = bus.Publish(new TestEventA(1));
            ctx.Handled.Should().BeFalse();
        }

        [Fact]
        public void Publish_ShouldReturnContextWithData()
        {
            using var bus = new Services.EventBus();
            var ctx = bus.Publish(new TestEventA(99));
            ctx.Data.Value.Should().Be(99);
        }

        [Fact]
        public void Publish_DifferentEventTypes_ShouldOnlyInvokeMatchingHandlers()
        {
            using var bus = new Services.EventBus();
            var aCalled = false;
            var bCalled = false;
            bus.Subscribe<TestEventA>(_ => aCalled = true);
            bus.Subscribe<TestEventB>(_ => bCalled = true);
            bus.Publish(new TestEventA(1));
            aCalled.Should().BeTrue();
            bCalled.Should().BeFalse();
        }

        [Fact]
        public void Publish_WhenHandlerThrows_ShouldContinueToNextHandler()
        {
            using var bus = new Services.EventBus();
            var secondCalled = false;
            bus.Subscribe<TestEventA>(_ => throw new System.InvalidOperationException("boom"), priority: 10);
            bus.Subscribe<TestEventA>(_ => secondCalled = true);
            bus.Publish(new TestEventA(1));
            secondCalled.Should().BeTrue();
        }
    }
}
