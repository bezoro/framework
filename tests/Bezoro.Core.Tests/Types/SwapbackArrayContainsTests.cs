using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(SwapbackArray<>))]
public class SwapbackArrayContainsTests
{
	[Fact]
	public void WhenArrayIsEmpty_ShouldReturnFalse()
	{
		var arr = new SwapbackArray<int>();

		arr.Contains(1).Should().BeFalse();
	}

	[Fact]
	public void WhenDefaultItemExists_ShouldReturnTrue()
	{
		// ReSharper disable once PreferConcreteValueOverDefault
		var arr = new SwapbackArray<int> { 1, 2, 3, default };

		// ReSharper disable once PreferConcreteValueOverDefault
		arr.Contains(default).Should().BeTrue();
	}

	[Fact]
	public void WhenDefaultItemNotFound_ShouldReturnFalse()
	{
		var arr = new SwapbackArray<int> { 1, 2, 3, 4 };

		// ReSharper disable once PreferConcreteValueOverDefault
		arr.Contains(default).Should().BeFalse();
	}

	[Fact]
	public void WhenItemExists_ShouldReturnTrue()
	{
		var arr = new SwapbackArray<int> { 1, 2, 3, 4 };

		arr.Contains(2).Should().BeTrue();
	}

	[Fact]
	public void WhenItemNotFound_ShouldReturnFalse()
	{
		var arr = new SwapbackArray<int> { 1, 2, 3, 4 };

		arr.Contains(5).Should().BeFalse();
	}

	[Fact]
	public void WhenNullItemExists_ShouldReturnTrue()
	{
		var arr = new SwapbackArray<int?> { 1, 2, 3, null };

		arr.Contains(null).Should().BeTrue();
	}

	[Fact]
	public void WhenNullItemNotFound_ShouldReturnFalse()
	{
		var arr = new SwapbackArray<int?> { 1, 2, 3, 4 };

		arr.Contains(null).Should().BeFalse();
	}
}
