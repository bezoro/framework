using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(SwapbackArray<>))]
public class SwapbackArrayIsEmptyTests
{
	[Fact]
	public void WhenArrayIsEmpty_ShouldReturnTrue()
	{
		var arr = new SwapbackArray<int>();

		arr.IsEmpty.Should().BeTrue();
	}

	[Fact]
	public void WhenArrayIsNotEmpty_ShouldReturnFalse()
	{
		var arr = new SwapbackArray<int> { 1, 2, 3 };

		arr.IsEmpty.Should().BeFalse();
	}
}
