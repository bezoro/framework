using System;
using Bezoro.ECS.Internal;
using FluentAssertions;
using Xunit;

namespace Bezoro.ECS.Tests.Internal;

public sealed class WorldResourceLifetimeTests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public void ReplacingWithSameInstance_RetainsLiveResource(bool boxed)
	{
		var store = new WorldResourceStore();
		var resource = new DisposableResource();
		store.Set(resource);
		if (boxed) store.SetBoxed(typeof(DisposableResource), resource);
		else store.Set(resource);

		resource.DisposeCount.Should().Be(0);
		store.Get<DisposableResource>().Should().BeSameAs(resource);
		store.Dispose();
		resource.DisposeCount.Should().Be(1);
	}

	[Fact]
	public void Dispose_DetachesAndDisposesEachOwnedResourceOnce()
	{
		var store = new WorldResourceStore();
		var resource = new DisposableResource();
		store.Set(resource);
		store.Dispose();
		store.Dispose();

		store.Count.Should().Be(0);
		resource.DisposeCount.Should().Be(1);
	}

	[Fact]
	public void Clear_AttemptsAllDisposalsAndDetachesFailedResource()
	{
		var store = new WorldResourceStore();
		var healthy = new DisposableResource();
		store.Set(new ThrowingDisposable());
		store.Set(healthy);
		Action clear = store.Clear;

		clear.Should().Throw<AggregateException>();
		healthy.DisposeCount.Should().Be(1);
		store.Count.Should().Be(0);
		store.Clear();
	}

	[Fact]
	public void RestoredStructResource_ProvidesStableWritableTypedReference()
	{
		var store = new WorldResourceStore();
		store.SetBoxed(typeof(Counter), new Counter { Value = 12 });
		ref Counter counter = ref store.Get<Counter>();
		counter.Value = 23;

		store.TryRead<Counter>(out var read).Should().BeTrue();
		read.Value.Should().Be(23);
		store.GetOrCreate(() => new Counter()).Value.Should().Be(23);
		store.GetOrCreate<Counter>().Value.Should().Be(23);
	}

	[Fact]
	public void ResourceFactoryReturningNull_DoesNotRegisterInvalidResource()
	{
		var store = new WorldResourceStore();
		Action create = () => store.GetOrCreate<DisposableResource>(() => null!);
		create.Should().Throw<InvalidOperationException>();
		store.Count.Should().Be(0);
	}

	[Fact]
	public void RestoredStructResource_RepeatedTypedReadsDoNotBox()
	{
		var store = new WorldResourceStore();
		store.SetBoxed(typeof(Counter), new Counter { Value = 1 });
		for (int warmup = 0; warmup < 10; warmup++) store.TryRead<Counter>(out _);
		long before = GC.GetAllocatedBytesForCurrentThread();
		int sum = 0;
		for (int iteration = 0; iteration < 10_000; iteration++)
		{
			store.TryRead<Counter>(out var counter);
			sum += counter.Value;
		}
		long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
		sum.Should().Be(10_000);
		allocated.Should().Be(0);
	}

	private struct Counter { public int Value; }
	private sealed class DisposableResource : IDisposable
	{
		public int DisposeCount { get; private set; }
		public void Dispose() => DisposeCount++;
	}
	private sealed class ThrowingDisposable : IDisposable
	{
		public void Dispose() => throw new InvalidOperationException("Dispose failed.");
	}
}
