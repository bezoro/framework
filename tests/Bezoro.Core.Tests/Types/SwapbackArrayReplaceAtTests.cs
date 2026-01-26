using System;
using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(SwapbackArray<>))]
public class SwapbackArrayReplaceAtTests
{
	[Fact]
	public void WhenIndexIsOutOfBounds_ShouldThrow()
	{
		var arr = new SwapbackArray<int> { 1, 2, 3, 4 };

		var act = () => arr.ReplaceAt(10, 5);

		act.Should().Throw<ArgumentOutOfRangeException>();
	}

	[Fact]
	public void WhenValidCall_ShouldIncrementVersion()
	{
		var  arr            = new SwapbackArray<int> { 1, 2, 3 };
		uint initialVersion = arr.Version;

		arr.ReplaceAt(1, 4);
		uint finalVersion = arr.Version;

		finalVersion.Should().NotBe(initialVersion);
		arr.Version.Should().Be(finalVersion);
	}

	[Fact]
	public void WhenValidCall_ShouldReplaceItem()
	{
		var arr = new SwapbackArray<int> { 1, 2, 3, 4 };

		arr.ReplaceAt(1, 5);

		arr.Count.Should().Be(4);
		arr.ToArray().Should().Equal(1, 5, 3, 4);
	}
}
