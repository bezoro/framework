using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.Events.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;
using Bezoro.Events.Tests.Services.Fixtures;

namespace Bezoro.Events.Tests.Services.EventBus;

[TestSubject(typeof(Events.Services.EventBus))]
public class EventBusConcurrencyTests
{
	[Fact]
	public void Enqueue_WhenCalledConcurrently_ShouldTrackAllEvents()
	{
		using var bus   = new Events.Services.EventBus();
		const int COUNT = 100;

		Parallel.For(0, COUNT, i => bus.Enqueue(new TestEventA(i)));

		bus.QueuedCount.Should().Be(COUNT);
	}

	[Fact]
	public void Publish_WhenCalledConcurrently_ShouldInvokeAllHandlers()
	{
		using var bus     = new Events.Services.EventBus();
		var       counter = 0;
		bus.Subscribe<TestEventA>(_ => Interlocked.Increment(ref counter));

		const int PUBLISH_COUNT = 100;
		Parallel.For(0, PUBLISH_COUNT, i => bus.Publish(new TestEventA(i)));

		counter.Should().Be(PUBLISH_COUNT);
	}

	[Fact]
	public void SubscribeAndPublish_WhenCalledConcurrently_ShouldNotThrow()
	{
		using var bus     = new Events.Services.EventBus();
		var       handles = new List<SubscriptionHandle>();

		var act = () => Parallel.For(
			0, 50, i =>
			{
				if (i % 2 == 0)
				{
					var h = bus.Subscribe<TestEventA>(_ => { });
					lock (handles)
					{
						handles.Add(h);
					}
				}
				else
				{
					bus.Publish(new TestEventA(i));
				}
			}
		);

		act.Should().NotThrow();
	}

	[Fact]
	public void Subscribe_WhenCalledConcurrently_ShouldNotLoseSubscriptions()
	{
		using var bus   = new Events.Services.EventBus();
		const int COUNT = 100;

		Parallel.For(0, COUNT, _ => bus.Subscribe<TestEventA>(_ => { }));

		bus.SubscriptionCount.Should().Be(COUNT);
	}
}
