using System;
using Bezoro.GameSystems.ActivationSystem.Services;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.GameSystems.Tests.ActivationSystem;

[TestSubject(typeof(ActivationService))]
public class ActivationServiceRegisterTests
{
	[Fact]
	public void WhenMultipleRegistrations_ShouldReturnUniqueHandles()
	{
		using var service = new ActivationService();

		var h1 = service.Register(() => { });
		var h2 = service.Register(() => { });
		var h3 = service.Register(() => { });

		h1.Should().NotBe(h2);
		h2.Should().NotBe(h3);
		h1.Should().NotBe(h3);
	}

	[Fact]
	public void WhenNullCallback_ShouldThrow()
	{
		using var service = new ActivationService();

		var act = () => service.Register(null!);

		act.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public void WhenRegistered_ActivatedCountShouldBeZero()
	{
		using var service = new ActivationService();

		service.Register(() => { });

		service.ActivatedCount.Should().Be(0);
	}

	[Fact]
	public void WhenRegistered_ShouldIncrementPendingCount()
	{
		using var service = new ActivationService();

		service.Register(() => { });
		service.Register(() => { });

		service.PendingCount.Should().Be(2);
	}

	[Fact]
	public void WhenRegisteredWithPriority_ShouldAcceptValue()
	{
		using var service = new ActivationService();

		var handle = service.Register(() => { }, 10);

		handle.IsValid.Should().BeTrue();
		service.PendingCount.Should().Be(1);
	}

	[Fact]
	public void WhenValidCallback_ShouldReturnValidHandle()
	{
		using var service = new ActivationService();

		var handle = service.Register(() => { });

		handle.IsValid.Should().BeTrue();
	}
}
