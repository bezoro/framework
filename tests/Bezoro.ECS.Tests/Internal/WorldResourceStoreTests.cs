using System;
using Bezoro.ECS.Internal;
using Bezoro.ECS.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.ECS.Tests.Internal;

[TestSubject(typeof(WorldResourceStore))]
public class WorldResourceStoreTests
{
	[Fact]
	public void Set_WhenReplacingDisposableResource_ShouldDisposePreviousInstance()
	{
		var store    = new WorldResourceStore();
		var previous = new DisposableResource();
		var current  = new DisposableResource();

		store.Set(previous);
		store.Set(current);

		previous.DisposeCount.Should().Be(1);
		current.DisposeCount.Should().Be(0);
	}

	[Fact]
	public void Clear_WhenStoreContainsDisposableResources_ShouldDisposeAllInstances()
	{
		var store    = new WorldResourceStore();
		var first    = new DisposableResource();
		var second   = new DisposableResource();

		store.Set(first);
		store.Set(second);

		store.Clear();

		first.DisposeCount.Should().Be(1);
		second.DisposeCount.Should().Be(1);
		store.Count.Should().Be(0);
	}

	[Fact]
	public void SetBoxed_WhenCapturingSnapshotRecords_ShouldRoundTripTypedResource()
	{
		var store = new WorldResourceStore();
		store.Set(new SnapshotCounterResource { Value = 42 });

		SnapshotResourceRecord record = store.CaptureSnapshotRecords().Should().ContainSingle().Subject;

		var restored = new WorldResourceStore();
		restored.SetBoxed(record.ResourceType, record.Value);

		restored.TryRead<SnapshotCounterResource>(out var resource).Should().BeTrue();
		resource.Value.Should().Be(42);
	}

	private sealed class DisposableResource : IDisposable
	{
		public int DisposeCount { get; private set; }

		public void Dispose() => DisposeCount++;
	}

	private sealed class SnapshotCounterResource
	{
		public int Value { get; init; }
	}
}
