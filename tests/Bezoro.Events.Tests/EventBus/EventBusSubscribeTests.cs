using System;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Events.Tests.EventBus;

[TestSubject(typeof(Services.EventBus))]
public static class EventBusSubscribeTests
{
	public class Unit
	{
		[Fact]
		public void Subscribe_MultipleTimes_ShouldReturnUniqueHandles()
		{
			using var bus = new Services.EventBus();
			var       h1  = bus.Subscribe<TestEventA>(_ => { });
			var       h2  = bus.Subscribe<TestEventA>(_ => { });
			var       h3  = bus.Subscribe<TestEventB>(_ => { });

			h1.Should().NotBe(h2);
			h2.Should().NotBe(h3);
		}

		[Fact]
		public void Subscribe_ShouldIncrementSubscriptionCount()
		{
			using var bus = new Services.EventBus();
			bus.Subscribe<TestEventA>(_ => { });
			bus.Subscribe<TestEventB>(_ => { });
			bus.SubscriptionCount.Should().Be(2);
		}

		[Fact]
		public void Subscribe_ShouldReturnValidHandle()
		{
			using var bus    = new Services.EventBus();
			var       handle = bus.Subscribe<TestEventA>(_ => { });
			handle.IsValid.Should().BeTrue();
		}

		[Fact]
		public void Subscribe_WithNullHandler_ShouldThrowArgumentNullException()
		{
			using var bus = new Services.EventBus();
			var       act = () => bus.Subscribe<TestEventA>(null!);
			act.Should().Throw<ArgumentNullException>();
		}
	}
}
