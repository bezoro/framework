using System;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.Events.Tests.Services.Fixtures;
using Xunit;

namespace Bezoro.Events.Tests.Services.EventBus;

public class EventBusLifecycleSafetyTests
{
	[Fact]
	public void Unsubscribe_WithHandleFromDifferentBus_DoesNotRemoveLocalHandler()
	{
		using var first = new Events.Services.EventBus();
		using var second = new Events.Services.EventBus();
		var foreign = first.Subscribe<TestEventA>(_ => { });
		var received = 0;
		var local = second.Subscribe<TestEventA>(_ => received++);

		Assert.NotEqual(foreign, local);
		Assert.False(second.Unsubscribe(foreign));
		second.Publish(new TestEventA(42));
		Assert.Equal(1, received);
	}

	[Fact]
	public async Task Dispose_RacingWithMutations_DoesNotRetainHandlersOrQueuedEvents()
	{
		for (var iteration = 0; iteration < 100; iteration++)
		{
			var bus = new Events.Services.EventBus();
			using var start = new Barrier(3);
			Task subscribe = Task.Run(() =>
			{
				start.SignalAndWait(TimeSpan.FromSeconds(5));
				try { bus.Subscribe<TestEventA>(_ => { }); }
				catch (ObjectDisposedException) { }
			});
			Task enqueue = Task.Run(() =>
			{
				start.SignalAndWait(TimeSpan.FromSeconds(5));
				try { bus.Enqueue(new TestEventA(1)); }
				catch (ObjectDisposedException) { }
			});
			start.SignalAndWait(TimeSpan.FromSeconds(5));
			bus.Dispose();
			await Task.WhenAll(subscribe, enqueue).WaitAsync(TimeSpan.FromSeconds(5));
			Assert.Equal(0, bus.SubscriptionCount);
			Assert.Equal(0, bus.QueuedCount);
		}
	}
}
