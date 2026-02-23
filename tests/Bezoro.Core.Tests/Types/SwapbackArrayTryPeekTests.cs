using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(SwapbackArray<>))]
public class SwapbackArrayTryPeekTests
{
	[Fact]
	public void WhenEmpty_WhenCalled_ShouldReturnFalse()
	{
		var arr = new SwapbackArray<int>();

		arr.TryPeek(out int _).Should().BeFalse();
	}

	[Fact]
	public void WhenNotEmpty_WhenCalled_ShouldReturnItem()
	{
		var arr = new SwapbackArray<int> { 1, 2, 3 };

		arr.TryPeek(out int value).Should().BeTrue();

		value.Should().Be(3);
	}
}
