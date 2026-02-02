using System;
using System.Collections.Generic;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Events.Tests.UnityEventBuses;

[TestSubject(typeof(Services.UnityEventBuses))]
public static class UnityEventBusesTests
{
    public class Unit
    {
        [Fact]
        public void FlushUpdate_ShouldDispatchUpdateQueue()
        {
            using var buses = new Services.UnityEventBuses();
            var received = new List<int>();
            buses.Update.Subscribe<TestEventA>(ctx => received.Add(ctx.Data.Value));
            buses.Update.Enqueue(new TestEventA(1));
            buses.Update.Enqueue(new TestEventA(2));

            buses.FlushUpdate().Should().Be(2);
            received.Should().ContainInOrder(1, 2);
        }

        [Fact]
        public void FlushFixedUpdate_ShouldNotDispatchOtherQueues()
        {
            using var buses = new Services.UnityEventBuses();
            var fixedReceived = new List<int>();
            var updateReceived = new List<int>();
            buses.FixedUpdate.Subscribe<TestEventA>(ctx => fixedReceived.Add(ctx.Data.Value));
            buses.Update.Subscribe<TestEventA>(ctx => updateReceived.Add(ctx.Data.Value));

            buses.FixedUpdate.Enqueue(new TestEventA(10));
            buses.Update.Enqueue(new TestEventA(20));

            buses.FlushFixedUpdate().Should().Be(1);
            fixedReceived.Should().ContainSingle().Which.Should().Be(10);
            updateReceived.Should().BeEmpty();
        }

        [Fact]
        public void Dispose_WhenOwningBuses_ShouldDisposeAll()
        {
            var buses = new Services.UnityEventBuses();
            buses.Dispose();

            Action act = () => buses.Update.Enqueue(new TestEventA(1));
            act.Should().Throw<ObjectDisposedException>();
        }
    }
}
