using System.Collections.Generic;
using Bezoro.Events.Services;
using Bezoro.Events.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Events.Tests.EventBus;

[TestSubject(typeof(Services.EventBus))]
public static class EventBusPriorityTests
{
    public class Unit
    {
        [Fact]
        public void Publish_HigherPriority_ShouldRunFirst()
        {
            using var bus = new Services.EventBus();
            var order = new List<string>();
            bus.Subscribe<TestEventA>(_ => order.Add("low"), priority: 0);
            bus.Subscribe<TestEventA>(_ => order.Add("high"), priority: 10);
            bus.Publish(new TestEventA(1));
            order.Should().ContainInOrder("high", "low");
        }

        [Fact]
        public void Publish_SamePriority_ShouldRunInSubscriptionOrder()
        {
            using var bus = new Services.EventBus();
            var order = new List<string>();
            bus.Subscribe<TestEventA>(_ => order.Add("first"), priority: 5);
            bus.Subscribe<TestEventA>(_ => order.Add("second"), priority: 5);
            bus.Subscribe<TestEventA>(_ => order.Add("third"), priority: 5);
            bus.Publish(new TestEventA(1));
            order.Should().ContainInOrder("first", "second", "third");
        }

        [Fact]
        public void Publish_MultiplePriorities_ShouldRunInCorrectOrder()
        {
            using var bus = new Services.EventBus();
            var order = new List<int>();
            bus.Subscribe<TestEventA>(_ => order.Add(1), priority: 1);
            bus.Subscribe<TestEventA>(_ => order.Add(100), priority: 100);
            bus.Subscribe<TestEventA>(_ => order.Add(50), priority: 50);
            bus.Subscribe<TestEventA>(_ => order.Add(-10), priority: -10);
            bus.Publish(new TestEventA(1));
            order.Should().ContainInOrder(100, 50, 1, -10);
        }
    }
}
