using System;
using System.Collections.Generic;
using Bezoro.ECS.Attributes;
using Bezoro.ECS.Services;
using Bezoro.GameSystems.ActivationSystem.Extensions;
using Bezoro.GameSystems.ActivationSystem.Services;
using Bezoro.GameSystems.ActivationSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.GameSystems.Tests.ActivationSystem;

[TestSubject(typeof(ActivationProcessingSystem))]
public class ActivationPipelineTests
{
	[Fact]
	public void Metadata_WhenInspectingActivationProcessingSystem_ShouldDeclareReadAndWriteAttributes()
	{
		var systemType = typeof(ActivationProcessingSystem);

		systemType.IsDefined(typeof(WritesAttribute<ActivationEntry>),                         true).Should().BeTrue();
		systemType.IsDefined(typeof(ReadsAttribute<ActivationCancellationRequest>),            true).Should().BeTrue();
		systemType.IsDefined(typeof(ReadsResourceAttribute<ActivationConfig>),                 true).Should().BeTrue();
		systemType.IsDefined(typeof(WritesResourceAttribute<ActivationRuntimeState>),          true).Should().BeTrue();
		systemType.IsDefined(typeof(WritesResourceAttribute<ActivationEventsResource>),        true).Should().BeTrue();
		systemType.IsDefined(typeof(WritesResourceAttribute<ActivationDispatchQueueResource>), true).Should().BeTrue();
	}

	[Fact]
	public void Tick_WhenActivationCompletes_ShouldPublishCompletionOncePerCompletionEdge()
	{
		var world = new World();
		world.SetResource(new ActivationConfig(2));
		world.AddActivationPipeline();
		var queue = world.GetOrCreateActivationCommandQueue();

		queue.Register(() => { });
		queue.Register(() => { });
		queue.Register(() => { });

		world.Tick(0f);
		world.Tick(0f);
		world.Tick(0f);

		queue.Register(() => { });
		world.Tick(0f);

		var events = world.GetResource<ActivationEventsResource>();
		events.Count.Should().Be(2);
	}

	[Fact]
	public void Tick_WhenCallbacksAreDispatchedThroughConfiguredDispatcher_ShouldDeferInvocationToDispatcher()
	{
		var dispatched = new List<Action>();
		var world      = new World();
		world.SetResource(new ActivationConfig(callbackDispatcher: callback => dispatched.Add(callback)));
		world.AddActivationPipeline();
		var queue   = world.GetOrCreateActivationCommandQueue();
		var invoked = false;

		queue.Register(() => invoked = true);
		world.Tick(0f);

		invoked.Should().BeFalse();
		dispatched.Should().HaveCount(1);

		dispatched[0]();
		invoked.Should().BeTrue();
	}

	[Fact]
	public void Tick_WhenCancellationWasQueuedBeforeProcessing_ShouldNotInvokeCallback()
	{
		var world = new World();
		world.AddActivationPipeline();
		var queue   = world.GetOrCreateActivationCommandQueue();
		var invoked = false;

		var handle = queue.Register(() => invoked = true);
		queue.Cancel(handle).Should().BeTrue();

		world.Tick(0f);

		invoked.Should().BeFalse();
		var runtime = world.GetResource<ActivationRuntimeState>();
		runtime.ActivatedCount.Should().Be(0);
		runtime.PendingCount.Should().Be(0);
	}

	[Fact]
	public void Tick_WhenMaxActivationsPerTickIsLimited_ShouldRespectBudgetPerTick()
	{
		var world = new World();
		world.SetResource(new ActivationConfig(2));
		world.AddActivationPipeline();
		var queue     = world.GetOrCreateActivationCommandQueue();
		var activated = 0;

		for (var i = 0; i < 5; i++)
			queue.Register(() => activated++);

		world.Tick(0f);
		activated.Should().Be(2);

		world.Tick(0f);
		activated.Should().Be(4);

		world.Tick(0f);
		activated.Should().Be(5);
	}

	[Fact]
	public void Tick_WhenPrioritiesDiffer_ShouldActivateInPriorityThenRegistrationOrder()
	{
		var order = new List<string>();
		var world = new World();
		world.SetResource(new ActivationConfig(1));
		world.AddActivationPipeline();
		var queue = world.GetOrCreateActivationCommandQueue();

		queue.Register(() => order.Add("low"));
		queue.Register(() => order.Add("high"),                 10);
		queue.Register(() => order.Add("medium"),               5);
		queue.Register(() => order.Add("same-priority-first"),  1);
		queue.Register(() => order.Add("same-priority-second"), 1);

		for (var i = 0; i < 5; i++)
			world.Tick(0f);

		order.Should().Equal("high", "medium", "same-priority-first", "same-priority-second", "low");

		var runtime = world.GetResource<ActivationRuntimeState>();
		runtime.ActivatedCount.Should().Be(5);
		runtime.PendingCount.Should().Be(0);
		runtime.IsComplete.Should().BeTrue();
	}
}
