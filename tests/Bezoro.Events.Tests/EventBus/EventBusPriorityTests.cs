using System.Collections.Generic;
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
			using var bus   = new Services.EventBus();
			var       order = new List<string>();
			bus.Subscribe<TestEventA>(_ => order.Add("low"),  0);
			bus.Subscribe<TestEventA>(_ => order.Add("high"), 10);
			bus.Publish(new TestEventA(1));
			order.Should().ContainInOrder("high", "low");
		}

		[Fact]
		public void Publish_MultiplePriorities_ShouldRunInCorrectOrder()
		{
			using var bus   = new Services.EventBus();
			var       order = new List<int>();
			bus.Subscribe<TestEventA>(_ => order.Add(1),   1);
			bus.Subscribe<TestEventA>(_ => order.Add(100), 100);
			bus.Subscribe<TestEventA>(_ => order.Add(50),  50);
			bus.Subscribe<TestEventA>(_ => order.Add(-10), -10);
			bus.Publish(new TestEventA(1));
			order.Should().ContainInOrder(100, 50, 1, -10);
		}

		[Fact]
		public void Publish_SamePriority_ShouldRunInSubscriptionOrder()
		{
			using var bus   = new Services.EventBus();
			var       order = new List<string>();
			bus.Subscribe<TestEventA>(_ => order.Add("first"),  5);
			bus.Subscribe<TestEventA>(_ => order.Add("second"), 5);
			bus.Subscribe<TestEventA>(_ => order.Add("third"),  5);
			bus.Publish(new TestEventA(1));
			order.Should().ContainInOrder("first", "second", "third");
		}
	}
}
