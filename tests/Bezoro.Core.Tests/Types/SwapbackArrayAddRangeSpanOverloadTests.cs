using System;
using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(SwapbackArray<>))]
public class SwapbackArrayAddRangeSpanOverloadTests
{
	[Fact]
	public void WhenEmpty_WhenCalled_ShouldNotIncrementVersion()
	{
		var  arr            = new SwapbackArray<int>();
		uint initialVersion = arr.Version;

		arr.AddRange(new Span<int>([]));

		arr.Version.Should().Be(initialVersion);
	}

	[Fact]
	public void WhenValid_WhenCalled_ShouldAddAllItems()
	{
		var arr = new SwapbackArray<int> { 1, 2 };

		arr.AddRange(new ReadOnlySpan<int>([3, 4, 5]));

		arr.ToArray().Should().Equal(1, 2, 3, 4, 5);
	}

	[Fact]
	public void WhenValid_WhenCalled_ShouldIncrementVersionOnce()
	{
		var  arr            = new SwapbackArray<int>();
		uint initialVersion = arr.Version;

		arr.AddRange(new ReadOnlySpan<int>([1, 2, 3]));
		uint finalVersion = arr.Version;

		finalVersion.Should().NotBe(initialVersion);
		arr.Version.Should().Be(finalVersion);
	}
}
