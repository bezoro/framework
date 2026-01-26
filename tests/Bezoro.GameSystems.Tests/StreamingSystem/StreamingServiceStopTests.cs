using System.Numerics;
using Bezoro.GameSystems.StreamingSystem.Services;
using Bezoro.GameSystems.StreamingSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.GameSystems.Tests.StreamingSystem;

[TestSubject(typeof(StreamingService))]
public class StreamingServiceStopTests
{
	[Fact]
	public void WhenNotRunning_ShouldBeNoOp()
	{
		using var system = new StreamingService();

		var act = () => system.Stop();

		act.Should().NotThrow();
	}

	[Fact]
	public void WhenRunning_ShouldSetIsRunningFalse()
	{
		using var system = new StreamingService();
		var       config = new StreamingConfig(() => Vector3.Zero);

		system.Start(config);
		system.Stop();

		system.IsRunning.Should().BeFalse();
	}
}
