using System.Collections.Generic;
using System.Linq;
using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(SwapbackArray<>))]
public class SwapbackArrayIntegrationTests
{
	[Fact]
	public void AddRangeThenClearThenReuse_ShouldNotLeakReferences()
	{
		var      arr  = new SwapbackArray<object>();
		object[] refs = Enumerable.Range(0, 100).Select(_ => new object()).ToArray();

		arr.AddRange((IEnumerable<object>)refs);
		arr.Clear();
		arr.Add(new());

		arr.Count.Should().Be(1);
		arr.Capacity.Should().Be(arr.MinimumArraySize);
	}

	[Fact]
	public void AddRangeThenRemoveAll_ShouldMaintainConsistency()
	{
		var arr = new SwapbackArray<int>();

		arr.AddRange(Enumerable.Range(0, 100));
		arr.RemoveAll(x => x % 2 == 0);

		arr.Count.Should().Be(50);
		arr.Should().OnlyContain(x => x % 2 == 1);
		arr.ToArray().Should().BeInAscendingOrder();
	}

	[Fact]
	public void BalancedChurn_ShouldMaintainCapacity()
	{
		var arr = new SwapbackArray<int>();

		for (var i = 0; i < 64; i++)
			arr.Add(i);

		uint stableCapacity = arr.Capacity;

		for (var i = 0; i < 256; i++)
		{
			arr.TryRemoveAt(0).Should().BeTrue();
			arr.Add(i + 64);
		}

		arr.Count.Should().Be(64);
		arr.Capacity.Should().Be(stableCapacity);
	}

	[Fact]
	public void EnsureCapacityThenTrimExcess_ShouldRoundTrip()
	{
		var arr = new SwapbackArray<int> { 1, 2, 3 };

		arr.EnsureCapacity(100);
		uint largeCapacity = arr.Capacity;
		arr.TrimExcess();

		arr.Capacity.Should().BeLessThan(largeCapacity);
		arr.Count.Should().Be(3);
		arr.ToArray().Should().Equal(1, 2, 3);
	}

	[Fact]
	public void GradualGrowth_ShouldNotOverAllocate()
	{
		var arr       = new SwapbackArray<int>();
		var nextValue = 0;

		for (var i = 0; i < 128; i++)
		{
			for (var j = 0; j < 4; j++)
				arr.Add(nextValue++);

			arr.TryRemoveAt(0).Should().BeTrue();
		}

		arr.Count.Should().BeGreaterThan(0u);
		arr.Capacity.Should().BeLessThanOrEqualTo(arr.Count * 2u);
	}
}
