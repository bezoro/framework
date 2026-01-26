using System;
using System.Numerics;
using Bezoro.GameSystems.StreamingSystem.Services;
using Bezoro.GameSystems.StreamingSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.GameSystems.Tests.StreamingSystem;

[TestSubject(typeof(StreamingService))]
public class StreamingServiceStartTests
{
	[Fact]
	public void WhenAlreadyRunning_ShouldBeNoOp()
	{
		using var system = new StreamingService();
		var       config = new StreamingConfig(() => Vector3.Zero);

		system.Start(config);
		var act = () => system.Start(config);

		act.Should().NotThrow();
		system.IsRunning.Should().BeTrue();
	}

	[Fact]
	public void WhenConfigHasNullReferencePosition_ShouldThrow()
	{
		using var system = new StreamingService();
		var       config = new StreamingConfig(null!);

		var act = () => system.Start(config);

		act.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public void WhenStreamOutDistanceLessThanStreamInDistance_ShouldThrow()
	{
		using var system = new StreamingService();
		var config = new StreamingConfig(
			() => Vector3.Zero,
			100f,
			50f // Invalid: less than stream in
		);

		var act = () => system.Start(config);

		act.Should().Throw<ArgumentException>()
		   .WithMessage("*StreamOutDistance*StreamInDistance*");
	}

	[Fact]
	public void WhenValidConfig_ShouldSetIsRunningTrue()
	{
		using var system = new StreamingService();
		var       config = new StreamingConfig(() => Vector3.Zero);

		system.Start(config);

		system.IsRunning.Should().BeTrue();
	}
}
