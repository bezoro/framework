using System;
using Xunit;

namespace Bezoro.Core.Tests.Collections.ArrayHelpers;

public class ArraySetupWithFirstElementUnitTests
{
	[Fact]
	public void SetupArrayWithFirstElement_WhenArrayIsEmpty_ThenInitializesWithGivenElementAndReturnsTrue()
	{
		// Arrange
		int[] array   = Array.Empty<int>();
		var   element = 42;

		// Act
		bool result = Common.Helpers.ArrayHelpers.SetupArrayWithFirstElement(ref array, element);

		// Assert
		Assert.True(result);
		Assert.NotNull(array);
		Assert.Equal(new[] { 42 }, array);
	}

	[Fact]
	public void SetupArrayWithFirstElement_WhenArrayIsNotEmpty_ThenItRemainsUnchangedAndReturnsFalse()
	{
		// Arrange
		int[] array   = { 10, 20, 30 };
		var   element = 42;

		// Act
		bool result = Common.Helpers.ArrayHelpers.SetupArrayWithFirstElement(ref array, element);

		// Assert
		Assert.False(result);
		Assert.Equal(new[] { 10, 20, 30 }, array);
	}

	[Fact]
	public void SetupArrayWithFirstElement_WhenArrayIsNotEmpty_ThenReturnsFalse()
	{
		// Arrange
		int[] array   = { 10, 20 };
		var   element = 42;

		// Act
		bool result = Common.Helpers.ArrayHelpers.SetupArrayWithFirstElement(ref array, element);

		// Assert
		Assert.False(result);
	}

	[Fact]
	public void SetupArrayWithFirstElement_WhenArrayIsNull_ThenInitializesWithGivenElementAndReturnsTrue()
	{
		// Arrange
		int[]? array   = null;
		var    element = 42;

		// Act
		bool result = Common.Helpers.ArrayHelpers.SetupArrayWithFirstElement(ref array, element);

		// Assert
		Assert.True(result);
		Assert.NotNull(array);
		Assert.Equal(new[] { 42 }, array);
	}
}
