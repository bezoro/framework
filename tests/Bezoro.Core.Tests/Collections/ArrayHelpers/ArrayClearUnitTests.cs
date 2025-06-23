using System;
using Bezoro.Core.Common.Helpers;
using Xunit;

namespace Bezoro.Core.Tests.Collections.ArrayHelpers;

public class ArrayClearUnitTests
{
	[Fact]
	public void Clear_WhenArrayExceedsParallelThreshold_UsesParallelMethodToResetAllElements()
	{
		// Arrange
		int threshold = ArrayHelper.ParallelThreshold;
		var array     = new int[threshold * 2];

		for (var i = 0 ; i < array.Length ; i++)
			array[i] = i + 1;

		// Act
		ArrayHelper.Clear(ref array);

		// Assert
		Assert.All(array, element => Assert.Equal(default, element));
	}

	[Fact]
	public void Clear_WhenArrayIsBelowParallelThreshold_UsesSequentialMethodToResetAllElements()
	{
		// Arrange
		int[] array = { 1, 2, 3, 4, 5 };

		// Act
		ArrayHelper.Clear(ref array);

		// Assert
		Assert.All(array, element => Assert.Equal(default, element));
	}

	[Fact]
	public void Clear_WhenArrayIsEmpty_DoesNotModifyArray()
	{
		// Arrange
		int[] array = Array.Empty<int>();

		// Act
		ArrayHelper.Clear(ref array);

		// Assert
		Assert.Empty(array);
	}

	[Fact]
	public void Clear_WhenArrayIsNull_DoesNotThrowException()
	{
		// Arrange
		int[]? array = null;

		// Act
		Exception? exception = Record.Exception(() => ArrayHelper.Clear(ref array));

		// Assert
		Assert.Null(exception);
	}
}
