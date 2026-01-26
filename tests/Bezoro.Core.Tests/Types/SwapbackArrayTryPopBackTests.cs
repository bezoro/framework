using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(SwapbackArray<>))]
public class SwapbackArrayTryPopBackTests
{
	[Fact]
	public void WhenEmpty_ShouldReturnFalse()
	{
		var arr = new SwapbackArray<int>();

		arr.TryPopBack(out int _).Should().BeFalse();
	}

	[Fact]
	public void WhenNotEmpty_ShouldReturnLastItem()
	{
		var arr = new SwapbackArray<int> { 1, 2, 3 };

		arr.TryPopBack(out int value).Should().BeTrue();
		value.Should().Be(3);
	}
}
