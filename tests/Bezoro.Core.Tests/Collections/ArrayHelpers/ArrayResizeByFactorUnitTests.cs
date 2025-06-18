using System;
using Xunit;

namespace Bezoro.Core.Tests.Collections.ArrayHelpers;

public class ArrayResizeByFactorUnitTests
{
	[Fact]
	public void ResizeByFactor_WhenArrayIsEmpty_ThenArraySizeRemainsZeroRegardlessOfFactor()
	{
		// Arrange
		int[]     array  = Array.Empty<int>();
		const int factor = 3;

		// Act
		Common.Helpers.ArrayHelpers.ResizeByFactor(ref array, factor);

		// Assert
		Assert.Equal(0, array.Length);
	}

	[Theory]
	[InlineData(1)]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(-2)]
	[InlineData(-3)]
	[InlineData(-4)]
	[InlineData(-5)]
	public void ResizeByFactor_WhenFactorIsOneOrLess_ThenArraySizeRemainsUnchanged(int factor)
	{
		// Arrange
		int[] array = { 1, 2, 3 };

		// Act
		Common.Helpers.ArrayHelpers.ResizeByFactor(ref array, factor);

		// Assert
		Assert.Equal(3, array.Length);
	}

	[Fact]
	public void ResizeByFactor_WhenFactorIsPositive_ThenArraySizeIncreasesByFactor()
	{
		// Arrange
		int[] array  = { 1, 2, 3 };
		var   factor = 2;

		// Act
		Common.Helpers.ArrayHelpers.ResizeByFactor(ref array, factor);

		// Assert
		Assert.Equal(6, array.Length);
	}
}
