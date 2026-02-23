using System.Collections.Generic;
using Bezoro.Events.Tests.Services.Fixtures;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Events.Tests.Services.EventBus;

[TestSubject(typeof(Events.Services.EventBus))]
public class EventBusPriorityTests
{
	[Fact]
	public void Publish_WhenHandlersHaveDifferentPriorities_ShouldRunHigherPriorityFirst()
	{
		using var bus   = new Events.Services.EventBus();
		var       order = new List<string>();
		bus.Subscribe<TestEventA>(_ => order.Add("low"));
		bus.Subscribe<TestEventA>(_ => order.Add("high"), 10);
		bus.Publish(new TestEventA(1));
		order.Should().ContainInOrder("high", "low");
	}

	[Fact]
	public void Publish_WhenHandlersSharePriority_ShouldRunInSubscriptionOrder()
	{
		using var bus   = new Events.Services.EventBus();
		var       order = new List<string>();
		bus.Subscribe<TestEventA>(_ => order.Add("first"),  5);
		bus.Subscribe<TestEventA>(_ => order.Add("second"), 5);
		bus.Subscribe<TestEventA>(_ => order.Add("third"),  5);
		bus.Publish(new TestEventA(1));
		order.Should().ContainInOrder("first", "second", "third");
	}

	[Fact]
	public void Publish_WhenHandlersUseMultiplePriorities_ShouldRunInPriorityOrder()
	{
		using var bus   = new Events.Services.EventBus();
		var       order = new List<int>();
		bus.Subscribe<TestEventA>(_ => order.Add(1),   1);
		bus.Subscribe<TestEventA>(_ => order.Add(100), 100);
		bus.Subscribe<TestEventA>(_ => order.Add(50),  50);
		bus.Subscribe<TestEventA>(_ => order.Add(-10), -10);
		bus.Publish(new TestEventA(1));
		order.Should().ContainInOrder(100, 50, 1, -10);
	}
}
