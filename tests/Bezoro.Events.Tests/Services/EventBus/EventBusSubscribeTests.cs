using System;
using Bezoro.Events.Tests.Services.Fixtures;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Events.Tests.Services.EventBus;

[TestSubject(typeof(Events.Services.EventBus))]
public class EventBusSubscribeTests
{
	[Fact]
	public void Subscribe_WhenCalled_ShouldIncrementSubscriptionCount()
	{
		using var bus = new Events.Services.EventBus();
		bus.Subscribe<TestEventA>(_ => { });
		bus.Subscribe<TestEventB>(_ => { });
		bus.SubscriptionCount.Should().Be(2);
	}

	[Fact]
	public void Subscribe_WhenCalled_ShouldReturnValidHandle()
	{
		using var bus    = new Events.Services.EventBus();
		var       handle = bus.Subscribe<TestEventA>(_ => { });
		handle.IsValid.Should().BeTrue();
	}

	[Fact]
	public void Subscribe_WhenCalledMultipleTimes_ShouldReturnUniqueHandles()
	{
		using var bus = new Events.Services.EventBus();
		var       h1  = bus.Subscribe<TestEventA>(_ => { });
		var       h2  = bus.Subscribe<TestEventA>(_ => { });
		var       h3  = bus.Subscribe<TestEventB>(_ => { });

		h1.Should().NotBe(h2);
		h2.Should().NotBe(h3);
	}

	[Fact]
	public void Subscribe_WhenHandlerIsNull_ShouldThrowArgumentNullException()
	{
		using var bus = new Events.Services.EventBus();
		var       act = () => bus.Subscribe<TestEventA>(null!);
		act.Should().Throw<ArgumentNullException>();
	}
}
