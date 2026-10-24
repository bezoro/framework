using System;
using System.Collections.Generic;
using Bezoro.Events.Abstractions;
using Bezoro.Events.Tests.Services.Fixtures;
using Bezoro.Events.Types;
using Xunit;

namespace Bezoro.Events.Tests.Services.EventBus;

public class EventBusCompatibilityTests
{
	[Fact]
	public void LegacyImplementation_ShouldCompileAndSupportTheOriginalContract()
	{
		using IEventBus bus = new LegacyEventBus();
		Assert.False(bus is IBoundedEventBus);
		var received = new List<int>();
		var handle = bus.Subscribe<TestEventA>(ctx => received.Add(ctx.Data.Value));
		Assert.Equal(1, bus.SubscriptionCount);
		Assert.Equal(1, bus.Publish(new TestEventA(1)).Data.Value);
		bus.Enqueue(new TestEventA(2));
		bus.Enqueue(new TestEventA(3));
		Assert.Equal(2, bus.QueuedCount);
		Assert.Equal(2, bus.FlushQueued());
		Assert.Equal(new[] { 1, 2, 3 }, received);
		Assert.Equal(0, bus.QueuedCount);
		Assert.True(bus.Unsubscribe(handle));
		Assert.Equal(0, bus.SubscriptionCount);
		bus.Dispose();
		Assert.Throws<ObjectDisposedException>(() => bus.FlushQueued());
	}

	[Fact]
	public void EventBus_ShouldSupportBothContractsOverTheSameQueue()
	{
		using var bus = new Events.Services.EventBus();
		IEventBus legacy = bus;
		IBoundedEventBus bounded = bus;
		var received = new List<int>();
		legacy.Subscribe<TestEventA>(ctx => received.Add(ctx.Data.Value));
		legacy.Enqueue(new TestEventA(1));
		bounded.Enqueue(new TestEventA(2));
		legacy.Enqueue(new TestEventA(3));

		Assert.Equal(1, bounded.FlushQueued(1));
		Assert.Equal(new[] { 1 }, received);
		Assert.Equal(2, legacy.QueuedCount);
		Assert.Equal(2, legacy.FlushQueued());
		Assert.Equal(new[] { 1, 2, 3 }, received);
		Assert.Equal(0, bounded.QueuedCount);
	}

	// Deliberately implements only the original IEventBus members.
	private sealed class LegacyEventBus : IEventBus
	{
		private readonly Events.Services.EventBus _bus = new();
		public int QueuedCount => _bus.QueuedCount;
		public int SubscriptionCount => _bus.SubscriptionCount;
		public bool Unsubscribe(SubscriptionHandle handle) => _bus.Unsubscribe(handle);
		public EventContext<TEvent> Publish<TEvent>(TEvent evt) where TEvent : struct => _bus.Publish(evt);
		public int FlushQueued() => _bus.FlushQueued();
		public SubscriptionHandle Subscribe<TEvent>(Action<EventContext<TEvent>> handler, int priority = 0)
			where TEvent : struct => _bus.Subscribe(handler, priority);
		public void Enqueue<TEvent>(TEvent evt) where TEvent : struct => _bus.Enqueue(evt);
		public void Dispose() => _bus.Dispose();
	}
}
