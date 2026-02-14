using Bezoro.ECS.Services;
using Bezoro.GameSystems.ActivationSystem.Extensions;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.GameSystems.Tests.ActivationSystem;

[TestSubject(typeof(ActivationWorldExtensions))]
public class ActivationWorldExtensionsTests
{
	[Fact]
	public void GetOrCreateActivationCommandQueue_WhenCalledMultipleTimes_ShouldReturnSameInstance()
	{
		var world = new World();

		var first = world.GetOrCreateActivationCommandQueue();
		var second = world.GetOrCreateActivationCommandQueue();

		ReferenceEquals(first, second).Should().BeTrue();
	}

	[Fact]
	public void AddActivationPipeline_WhenInvoked_ShouldWireSystemsForEndToEndActivation()
	{
		var world = new World();
		world.AddActivationPipeline();
		var queue = world.GetOrCreateActivationCommandQueue();
		var called = false;

		queue.Register(() => called = true);
		world.Tick(0f);

		called.Should().BeTrue();
	}
}
