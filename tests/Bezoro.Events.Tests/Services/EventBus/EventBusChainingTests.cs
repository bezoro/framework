using System.Collections.Generic;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;
using Bezoro.Events.Tests.Services.Fixtures;

namespace Bezoro.Events.Tests.Services.EventBus;

[TestSubject(typeof(Events.Services.EventBus))]
public class EventBusChainingTests
{
	[Fact]
	public void Enqueue_WhenCalledDuringPublish_ShouldNotDispatchUntilFlush()
	{
		using var bus     = new Events.Services.EventBus();
		var       bCalled = false;

		bus.Subscribe<TestEventA>(_ => bus.Enqueue(new TestEventB("queued")));
		bus.Subscribe<TestEventB>(_ => bCalled = true);

		bus.Publish(new TestEventA(1));
		bCalled.Should().BeFalse();
		bus.QueuedCount.Should().Be(1);

		bus.FlushQueued();
		bCalled.Should().BeTrue();
	}

	[Fact]
	public void Publish_WhenEnemyDeathScenarioOccurs_ShouldChainThroughExperienceAndLevelUp()
	{
		using var bus         = new Events.Services.EventBus();
		var       playerExp   = 0;
		var       playerLevel = 1;

		bus.Subscribe<TestEventA>(ctx =>
									  bus.Publish(new TestEventB($"{ctx.Data.Value}"))
		);

		bus.Subscribe<TestEventB>(ctx =>
			{
				playerExp += int.Parse(ctx.Data.Message);
				if (playerExp >= 100)
					bus.Publish(new TestEventC(playerLevel + 1));
			}
		);

		bus.Subscribe<TestEventC>(ctx => playerLevel = (int)ctx.Data.Amount);

		bus.Publish(new TestEventA(150));
		playerExp.Should().Be(150);
		playerLevel.Should().Be(2);
	}

	[Fact]
	public void Publish_WhenHandlerPublishesNewEvent_ShouldChainInline()
	{
		using var bus   = new Events.Services.EventBus();
		var       order = new List<string>();

		bus.Subscribe<TestEventA>(ctx =>
			{
				order.Add("A");
				bus.Publish(new TestEventB("from A"));
			}
		);

		bus.Subscribe<TestEventB>(ctx => order.Add("B"));

		bus.Publish(new TestEventA(1));
		order.Should().ContainInOrder("A", "B");
	}

	[Fact]
	public void Publish_WhenThreeLevelChainOccurs_ShouldExecuteAllInline()
	{
		using var bus   = new Events.Services.EventBus();
		var       order = new List<string>();

		bus.Subscribe<TestEventA>(_ =>
			{
				order.Add("A");
				bus.Publish(new TestEventB("chained"));
			}
		);

		bus.Subscribe<TestEventB>(_ =>
			{
				order.Add("B");
				bus.Publish(new TestEventC(3.14));
			}
		);

		bus.Subscribe<TestEventC>(_ => order.Add("C"));

		bus.Publish(new TestEventA(1));
		order.Should().ContainInOrder("A", "B", "C");
	}
}
