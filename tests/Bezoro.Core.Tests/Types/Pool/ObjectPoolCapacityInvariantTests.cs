using System;
using Bezoro.Core.Types.Pool;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types.Pool;

[TestSubject(typeof(ObjectPool<>))]
public class ObjectPoolCapacityInvariantTests
{
	[Fact]
	public void Rent_WhenFactoryThrows_ShouldReleaseReservedCapacity()
	{
		var policy = new ThrowingCreatePolicy(new InvalidOperationException("factory failed"));
		var pool = new ObjectPool<object>(policy, new() { MaxCapacity = 1 });

		Action first = () => pool.Rent();
		Action second = () => pool.Rent();

		first.Should().Throw<InvalidOperationException>().WithMessage("factory failed");
		second.Should().Throw<InvalidOperationException>().WithMessage("factory failed");
		pool.TotalCount.Should().Be(0);
		policy.CreateCount.Should().Be(2);
	}

	[Fact]
	public void Rent_WhenAvailableItemFailsValidation_ShouldReleaseItsCapacityOnce()
	{
		var policy = new TrackingPoolPolicy { ValidateResult = true };
		var pool = new ObjectPool<object>(policy, new() { MaxCapacity = 1, TrackStatistics = true, ValidateOnRent = true });
		var item = pool.Rent();
		pool.Return(item).Should().BeTrue();
		policy.ValidateResult = false;

		pool.TryRent(out var rented).Should().BeFalse();
		rented.Should().BeNull();
		pool.TotalCount.Should().Be(0);
		pool.Statistics.TotalDiscarded.Should().Be(1);
		policy.DiscardCount.Should().Be(1);
	}

	[Fact]
	public void Return_WhenResetRejectsOwnedItem_ShouldReleaseItsCapacityOnce()
	{
		var policy = new TrackingPoolPolicy { ResetResult = false };
		var pool = new ObjectPool<object>(policy, new() { MaxCapacity = 1, TrackStatistics = true });
		var item = pool.Rent();

		pool.Return(item).Should().BeFalse();

		pool.TotalCount.Should().Be(0);
		pool.Statistics.TotalDiscarded.Should().Be(1);
		policy.DiscardCount.Should().Be(1);
	}
}
