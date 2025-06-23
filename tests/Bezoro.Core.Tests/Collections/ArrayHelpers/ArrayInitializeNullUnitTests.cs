using System;
using System.Collections.Generic;
using Bezoro.Core.Common.Helpers;
using Xunit;

namespace Bezoro.Core.Tests.Collections.ArrayHelpers;

public class ArrayInitializeNullUnitTests
{
	public static IEnumerable<object?[]> InputArrayReturnsExpectedTestCases =>
		new List<object?[]>
		{
			new object?[] { null, Array.Empty<int>() },
			new object?[] { new[] { 1, 2, 3 }, new[] { 1, 2, 3 } },
			new object?[] { Array.Empty<int>(), Array.Empty<int>() }
		};

	[Fact]
	public void InitializeNullArray_WhenInitializingArrays_ThenReferenceChangesOnlyForNullArrays()
	{
		// Arrange
		int[]? nullArray    = null;
		var    nonNullArray = new[] { 1, 2, 3 };

		int[] originalNonNullReference = nonNullArray;

		// Act
		ArrayHelper.InitializeNullArray(ref nullArray);
		ArrayHelper.InitializeNullArray(ref nonNullArray);

		// Assert
		Assert.NotNull(nullArray);
		Assert.Same(originalNonNullReference, nonNullArray);
	}

	[Theory]
	[MemberData(nameof(InputArrayReturnsExpectedTestCases))]
	public void InitializeNullArray_WhenInputArrayProvided_ThenReturnsExpectedResult(int[]? input, int[] expected)
	{
		// Act
		ArrayHelper.InitializeNullArray(ref input);

		// Assert
		Assert.Equal(expected, input);
	}

	[Fact]
	public void InitializeNullArray_WhenInputIsEmpty_ThenRemainsEmpty()
	{
		// Arrange
		int[] input = Array.Empty<int>();

		// Act
		ArrayHelper.InitializeNullArray(ref input);

		// Assert
		Assert.NotNull(input);
		Assert.Empty(input);
	}

	[Fact]
	public void InitializeNullArray_WhenInputIsNotNull_ThenContentsRemainUnchanged()
	{
		// Arrange
		var input    = new[] { 1, 2, 3 };
		var expected = new[] { 1, 2, 3 };

		// Act
		ArrayHelper.InitializeNullArray(ref input);

		// Assert
		Assert.Equal(expected, input);
	}

	[Fact]
	public void InitializeNullArray_WhenInputIsNull_ThenInitializesToEmptyArray()
	{
		// Arrange
		int[]? input = null;

		// Act
		ArrayHelper.InitializeNullArray(ref input);

		// Assert
		Assert.NotNull(input);
		Assert.Empty(input);
	}
}
