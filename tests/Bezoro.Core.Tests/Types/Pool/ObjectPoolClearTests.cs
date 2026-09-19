using System;
using System.Collections.Generic;
using System.Reflection;
using Bezoro.Core.Abstractions;
using Bezoro.Core.Types;
using Bezoro.Core.Types.Pool;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types.Pool;

[TestSubject(typeof(ObjectPool<>))]
public class ObjectPoolClearTests
{
	[Fact]
	public void ObjectPoolClear_WhenCalled_ShouldShouldRemoveAllItems()
	{
		var pool = new ObjectPool<object>(
			() => new(),
			new() { InitialCapacity = 5 }
		);

		pool.Clear();

		pool.AvailableCount.Should().Be(0);
		pool.TotalCount.Should().Be(0);
	}

	[Fact]
	public void WithDisposeTrue_WhenCalled_ShouldDisposeItems()
	{
		var items = new List<DisposableObject>();
		var discardCount = 0;
		var policy = new PoolPolicy<DisposableObject>(
			() =>
			{
				var item = new DisposableObject();
				items.Add(item);
				return item;
			},
			onDiscard: _ => discardCount++
		);
		var pool = new ObjectPool<DisposableObject>(
			policy,
			new() { InitialCapacity = 3, MaxCapacity = -1 }
		);

		pool.Clear();

		items.Should().HaveCount(3).And.OnlyHaveUniqueItems();
		items.Should().OnlyContain(x => x.IsDisposed);
		items.Should().OnlyContain(x => x.DisposeCount == 1);
		pool.TotalCount.Should().Be(0);
		discardCount.Should().Be(0);
	}

	[Fact]
	public void ClearWithPolicyDiscard_WhenCalled_ShouldDiscardEachAvailableItemAndReleaseCapacity()
	{
		var policy = new TrackingPoolPolicy();
		var pool = new ObjectPool<object>(policy, new() { InitialCapacity = 3, TrackStatistics = true });

		pool.ClearWithPolicyDiscard();

		pool.AvailableCount.Should().Be(0);
		pool.TotalCount.Should().Be(0);
		policy.DiscardCount.Should().Be(3);
	}

	[Fact]
	public void Clear_WhenUsingCompatibilityOverload_ShouldBeObsoleteAndForwardToPolicyDiscard()
	{
		const string obsoleteMessage = "Use Clear() or ClearWithPolicyDiscard() instead.";
		var policy = new TrackingPoolPolicy();
		IPool<object> pool = new ObjectPool<object>(policy, new() { InitialCapacity = 1 });

		foreach (var type in new[] { typeof(IPool<object>), typeof(ObjectPool<object>) })
		{
			var method = type.GetMethod(
				nameof(IPool<object>.Clear),
				BindingFlags.Instance | BindingFlags.Public,
				[typeof(bool)]
			);
			method.Should().NotBeNull();
			var attribute = method!.GetCustomAttribute<ObsoleteAttribute>();
			attribute.Should().NotBeNull();
			attribute!.Message.Should().Be(obsoleteMessage);
		}

#pragma warning disable CS0618 // Dedicated compatibility-shim assertion.
		pool.Clear(disposeItems: false);
#pragma warning restore CS0618

		pool.AvailableCount.Should().Be(0);
		pool.TotalCount.Should().Be(0);
		policy.DiscardCount.Should().Be(1);
	}

	[Fact]
	public void Clear_WhenLegacyInterfaceImplementerUsesCanonicalMethods_ShouldForwardToBooleanOverload()
	{
		var legacyPool = new LegacyClearPool<object>();
		IPool<object> pool = legacyPool;

		pool.Clear();
		pool.ClearWithPolicyDiscard();

		legacyPool.ClearArguments.Should().Equal(true, false);
	}
}
