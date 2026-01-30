using System;
using Bezoro.GameSystems.TimerSystem.Services;
using Bezoro.GameSystems.TimerSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.GameSystems.Tests.TimerSystem;

[TestSubject(typeof(TimerService))]
public class TimerServiceCreateTests
{
	[Fact]
	public void WhenValidDuration_ShouldReturnValidHandle()
	{
		using var service = new TimerService();

		var handle = service.Create(TimeSpan.FromSeconds(5));

		handle.IsValid.Should().BeTrue();
	}

	[Fact]
	public void WhenMultipleCreated_ShouldReturnUniqueHandles()
	{
		using var service = new TimerService();

		var h1 = service.Create(TimeSpan.FromSeconds(1));
		var h2 = service.Create(TimeSpan.FromSeconds(2));
		var h3 = service.Create(TimeSpan.FromSeconds(3));

		h1.Should().NotBe(h2);
		h2.Should().NotBe(h3);
		h1.Should().NotBe(h3);
	}

	[Fact]
	public void WhenZeroDuration_ShouldThrow()
	{
		using var service = new TimerService();

		var act = () => service.Create(TimeSpan.Zero);

		act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("duration");
	}

	[Fact]
	public void WhenNegativeDuration_ShouldThrow()
	{
		using var service = new TimerService();

		var act = () => service.Create(TimeSpan.FromSeconds(-1));

		act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("duration");
	}

	[Fact]
	public void WhenCreated_ShouldBeInRunningState()
	{
		using var service = new TimerService();

		var handle = service.Create(TimeSpan.FromSeconds(5));

		service.TryGetInfo(handle, out var info).Should().BeTrue();
		info.State.Should().Be(TimerState.Running);
	}

	[Fact]
	public void WhenCreatedWithCallback_ShouldStoreCallback()
	{
		using var service = new TimerService();
		bool      called  = false;

		var handle = service.Create(TimeSpan.FromSeconds(5), _ => called = true);

		handle.IsValid.Should().BeTrue();
		called.Should().BeFalse();
	}

	[Fact]
	public void WhenCreated_ShouldIncrementActiveCount()
	{
		using var service = new TimerService();

		service.Create(TimeSpan.FromSeconds(1));
		service.Create(TimeSpan.FromSeconds(2));

		service.ActiveCount.Should().Be(2);
	}
}
