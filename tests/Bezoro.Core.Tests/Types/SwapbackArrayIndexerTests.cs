using System;
using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(SwapbackArray<>))]
public class SwapbackArrayIndexerTests
{
	[Fact]
	public void WhenGetOutOfBounds_ShouldThrow()
	{
		int[] values = [1, 2, 3, 4];
		var   arr    = new SwapbackArray<int>(values);

		var act = () => arr[(uint)values.Length];

		act.Should().Throw<ArgumentOutOfRangeException>();
	}

	[Fact]
	public void WhenGetValidIndex_ShouldReturnItem()
	{
		var arr = new SwapbackArray<int> { 1, 2, 3, 4 };

		arr[0].Should().Be(1);
		arr[1].Should().Be(2);
		arr[2].Should().Be(3);
		arr[3].Should().Be(4);
	}

	[Fact]
	public void WhenSetOutOfBounds_ShouldThrow()
	{
		int[] values = [1, 2, 3, 4];
		var   arr    = new SwapbackArray<int>(values);

		var act = () => arr[(uint)values.Length] = 10;

		act.Should().Throw<ArgumentOutOfRangeException>();
	}

	[Fact]
	public void WhenSetValidIndex_ShouldIncrementVersion()
	{
		var  arr            = new SwapbackArray<int> { 1, 2, 3 };
		uint initialVersion = arr.Version;

		arr[1] = 10;
		uint finalVersion = arr.Version;

		finalVersion.Should().NotBe(initialVersion);
		arr.Version.Should().Be(finalVersion);
	}

	[Fact]
	public void WhenSetValidIndex_ShouldSetItem()
	{
		var arr = new SwapbackArray<int> { 1, 2, 3, 4 };

		arr[1] = 10;

		arr[1].Should().Be(10);
	}
}
