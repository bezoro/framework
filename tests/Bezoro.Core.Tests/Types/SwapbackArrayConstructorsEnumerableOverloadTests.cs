using System;
using System.Collections.Generic;
using System.Linq;
using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(SwapbackArray<>))]
public class SwapbackArrayConstructorsEnumerableOverloadTests
{
	[Fact]
	public void WhenEmpty_ShouldCreateEmptyArray()
	{
		var values = GetNonCollectionEnumerable();
		var arr    = new SwapbackArray<int>(values);

		arr.Count.Should().Be(0);
		arr.Capacity.Should().Be(arr.MinimumArraySize);
	}

	[Fact]
	public void WhenLargeEnumerable_ShouldGrowCapacityAsNeeded()
	{
		var values = GetNonCollectionEnumerable(Enumerable.Range(0, 100).ToArray());
		var arr    = new SwapbackArray<int>(values);

		arr.Count.Should().Be(100);
		arr.ToArray().Should().Equal(Enumerable.Range(0, 100));
	}

	[Fact]
	public void WhenNull_ShouldThrow()
	{
		var act = () => new SwapbackArray<int>((IEnumerable<int>)null!);

		act.Should().Throw<ArgumentNullException>().WithParameterName("collection");
	}

	[Fact]
	public void WhenValid_ShouldCopyElements()
	{
		var values = GetNonCollectionEnumerable(1, 2, 3, 4);
		var arr    = new SwapbackArray<int>(values);

		arr.ToArray().Should().Equal(1, 2, 3, 4);
		arr.Count.Should().Be(4);
	}

	private static IEnumerable<int> GetNonCollectionEnumerable(params int[] values)
	{
		foreach (int value in values)
			yield return value;
	}
}
