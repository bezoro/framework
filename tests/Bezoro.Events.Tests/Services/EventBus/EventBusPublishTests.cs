using System;
using Bezoro.Events.Tests.Services.Fixtures;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Events.Tests.Services.EventBus;

[TestSubject(typeof(Events.Services.EventBus))]
public class EventBusPublishTests
{
	[Fact]
	public void Publish_WhenCalled_ShouldReturnContextWithData()
	{
		using var bus = new Events.Services.EventBus();
		var       ctx = bus.Publish(new TestEventA(99));
		ctx.Data.Value.Should().Be(99);
	}

	[Fact]
	public void Publish_WhenHandlerIsSubscribed_ShouldInvokeHandler()
	{
		using var bus      = new Events.Services.EventBus();
		var       received = 0;
		bus.Subscribe<TestEventA>(ctx => received = ctx.Data.Value);
		bus.Publish(new TestEventA(42));
		received.Should().Be(42);
	}

	[Fact]
	public void Publish_WhenHandlerThrows_ShouldContinueToNextHandler()
	{
		using var bus          = new Events.Services.EventBus();
		var       secondCalled = false;
		bus.Subscribe<TestEventA>(_ => throw new InvalidOperationException("boom"), 10);
		bus.Subscribe<TestEventA>(_ => secondCalled = true);
		bus.Publish(new TestEventA(1));
		secondCalled.Should().BeTrue();
	}

	[Fact]
	public void Publish_WhenMultipleHandlersAreSubscribed_ShouldInvokeAllHandlers()
	{
		using var bus   = new Events.Services.EventBus();
		var       count = 0;
		bus.Subscribe<TestEventA>(_ => count++);
		bus.Subscribe<TestEventA>(_ => count++);
		bus.Publish(new TestEventA(1));
		count.Should().Be(2);
	}

	[Fact]
	public void Publish_WhenNoSubscribersAreRegistered_ShouldReturnContextWithHandledFalse()
	{
		using var bus = new Events.Services.EventBus();
		var       ctx = bus.Publish(new TestEventA(1));
		ctx.Handled.Should().BeFalse();
	}

	[Fact]
	public void Publish_WhenPublishingDifferentEventTypes_ShouldOnlyInvokeMatchingHandlers()
	{
		using var bus     = new Events.Services.EventBus();
		var       aCalled = false;
		var       bCalled = false;
		bus.Subscribe<TestEventA>(_ => aCalled = true);
		bus.Subscribe<TestEventB>(_ => bCalled = true);
		bus.Publish(new TestEventA(1));
		aCalled.Should().BeTrue();
		bCalled.Should().BeFalse();
	}
}
