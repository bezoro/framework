using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Events.Tests.EventBus;

[TestSubject(typeof(Services.EventBus))]
public static class EventBusCancellationTests
{
	public class Unit
	{
		[Fact]
		public void Publish_WhenHandlerSetsHandled_ShouldReturnHandledContext()
		{
			using var bus = new Services.EventBus();
			bus.Subscribe<TestEventA>(ctx => ctx.Handled = true);
			var ctx = bus.Publish(new TestEventA(1));
			ctx.Handled.Should().BeTrue();
		}

		[Fact]
		public void Publish_WhenHandlerSetsHandled_ShouldStopPropagation()
		{
			using var bus          = new Services.EventBus();
			var       secondCalled = false;
			bus.Subscribe<TestEventA>(ctx => ctx.Handled = true, 10);
			bus.Subscribe<TestEventA>(_ => secondCalled  = true, 0);
			bus.Publish(new TestEventA(1));
			secondCalled.Should().BeFalse();
		}

		[Fact]
		public void Publish_WhenLowPrioritySetsHandled_HighPriorityAlreadyRan()
		{
			using var bus     = new Services.EventBus();
			var       highRan = false;
			bus.Subscribe<TestEventA>(_ => highRan       = true, 10);
			bus.Subscribe<TestEventA>(ctx => ctx.Handled = true, 0);
			bus.Publish(new TestEventA(1));
			highRan.Should().BeTrue();
		}

		[Fact]
		public void Publish_WhenNoHandlerSetsHandled_ShouldReturnUnhandledContext()
		{
			using var bus = new Services.EventBus();
			bus.Subscribe<TestEventA>(_ => { });
			var ctx = bus.Publish(new TestEventA(1));
			ctx.Handled.Should().BeFalse();
		}
	}
}
