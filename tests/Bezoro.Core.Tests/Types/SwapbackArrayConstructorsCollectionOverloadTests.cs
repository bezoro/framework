using System;
using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(SwapbackArray<>))]
public class SwapbackArrayConstructorsCollectionOverloadTests
{
	[Fact]
	public void WhenEmpty_ShouldUseMinimumCapacity()
	{
		var arr = new SwapbackArray<int>(Array.Empty<int>());

		arr.Capacity.Should().Be(4);
		arr.Count.Should().Be(0);
	}

	[Fact]
	public void WhenNull_ShouldThrow()
	{
		var act = () => new SwapbackArray<int>(null!);

		act.Should().Throw<ArgumentNullException>().WithParameterName("collection");
	}

	[Fact]
	public void WhenValid_ShouldCopyElements()
	{
		int[] values = [1, 2, 3, 4];

		var arr = new SwapbackArray<int>(values);

		arr.ToArray().Should().Equal(values);
		arr.Count.Should().Be(4);
		arr.Capacity.Should().BeGreaterThanOrEqualTo(4);
	}
}
