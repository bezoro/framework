using System;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Events.Tests.EventBus;

[TestSubject(typeof(Services.EventBus))]
public static class EventBusDisposeTests
{
	public class Unit
	{
		[Fact]
		public void Dispose_MultipleTimes_ShouldNotThrow()
		{
			var bus = new Services.EventBus();
			bus.Dispose();
			var act = () => bus.Dispose();
			act.Should().NotThrow();
		}

		[Fact]
		public void Dispose_ShouldClearSubscriptions()
		{
			var bus = new Services.EventBus();
			bus.Subscribe<TestEventA>(_ => { });
			bus.Dispose();
			bus.SubscriptionCount.Should().Be(0);
		}

		[Fact]
		public void Enqueue_AfterDispose_ShouldThrowObjectDisposedException()
		{
			var bus = new Services.EventBus();
			bus.Dispose();
			var act = () => bus.Enqueue(new TestEventA(1));
			act.Should().Throw<ObjectDisposedException>();
		}

		[Fact]
		public void FlushQueued_AfterDispose_ShouldThrowObjectDisposedException()
		{
			var bus = new Services.EventBus();
			bus.Dispose();
			var act = () => bus.FlushQueued();
			act.Should().Throw<ObjectDisposedException>();
		}

		[Fact]
		public void Publish_AfterDispose_ShouldThrowObjectDisposedException()
		{
			var bus = new Services.EventBus();
			bus.Dispose();
			var act = () => bus.Publish(new TestEventA(1));
			act.Should().Throw<ObjectDisposedException>();
		}

		[Fact]
		public void Subscribe_AfterDispose_ShouldThrowObjectDisposedException()
		{
			var bus = new Services.EventBus();
			bus.Dispose();
			var act = () => bus.Subscribe<TestEventA>(_ => { });
			act.Should().Throw<ObjectDisposedException>();
		}

		[Fact]
		public void Unsubscribe_AfterDispose_ShouldThrowObjectDisposedException()
		{
			var bus    = new Services.EventBus();
			var handle = bus.Subscribe<TestEventA>(_ => { });
			bus.Dispose();
			var act = () => bus.Unsubscribe(handle);
			act.Should().Throw<ObjectDisposedException>();
		}
	}
}
