using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.Events.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Events.Tests.EventBus;

[TestSubject(typeof(Services.EventBus))]
public static class EventBusConcurrencyTests
{
	public class Unit
	{
		[Fact]
		public void ConcurrentEnqueue_ShouldTrackAllEvents()
		{
			using var bus   = new Services.EventBus();
			const int count = 100;

			Parallel.For(0, count, i => bus.Enqueue(new TestEventA(i)));

			bus.QueuedCount.Should().Be(count);
		}

		[Fact]
		public void ConcurrentPublish_ShouldInvokeAllHandlers()
		{
			using var bus     = new Services.EventBus();
			var       counter = 0;
			bus.Subscribe<TestEventA>(_ => Interlocked.Increment(ref counter));

			const int publishCount = 100;
			Parallel.For(0, publishCount, i => bus.Publish(new TestEventA(i)));

			counter.Should().Be(publishCount);
		}

		[Fact]
		public void ConcurrentSubscribeAndPublish_ShouldNotThrow()
		{
			using var bus     = new Services.EventBus();
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
		public void ConcurrentSubscribes_ShouldNotLoseSubscriptions()
		{
			using var bus   = new Services.EventBus();
			const int count = 100;

			Parallel.For(0, count, _ => bus.Subscribe<TestEventA>(_ => { }));

			bus.SubscriptionCount.Should().Be(count);
		}
	}
}
