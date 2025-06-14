using Xunit;

namespace Bezoro.Core.Tests.Collections.ArrayHelpers;

public class ArrayAddUniqueUnitTests
{
	[Fact]
	public void AddUnique_DuplicateDetection_AddsUniqueElementButIgnoresDuplicate()
	{
		// Arrange
		string?[]     array         = { "Element1", "Element2" };
		var           initialLength = array.Length;
		const string? element       = "Element3";

		// Act
		Common.Helpers.ArrayHelpers.AddUnique(ref array, element, out _);

		// Assert
		Assert.NotNull(array);
		Assert.True(array.Length > initialLength);
		Assert.Equal(element, array[initialLength]);

		// Attempt to add a duplicate element
		var newLength = array.Length;
		Common.Helpers.ArrayHelpers.AddUnique(ref array, element, out _);

		// Assert that duplicate is not added
		Assert.Equal(newLength, array.Length);
	}

	[Fact]
	public void AddUnique_WhenArrayExceedsParallelThreshold_UsesParallelMethodAndReturnsCorrectIndex()
	{
		// Arrange
		var           array         = new string?[Common.Helpers.ArrayHelpers.ParallelThreshold * 2];
		var           initialLength = array.Length;
		const string? elementToAdd  = "Element999";

		for (var i = 0 ; i < array.Length ; i++)
			array[i] = $"Element{i + 1}";

		// Act
		Common.Helpers.ArrayHelpers.AddUnique(ref array, elementToAdd, out var index);

		// Assert
		Assert.NotNull(array);
		Assert.Contains(elementToAdd, array);
		Assert.Equal(initialLength, index);
		Assert.True(array.Length > initialLength);
	}

	[Fact]
	public void AddUnique_WhenArrayIsBelowThreshold_UsesSequentialMethodAndReturnsCorrectIndex()
	{
		// Arrange
		string?[]     array   = { "Element1" };
		const string? element = "Element2";

		// Act
		Common.Helpers.ArrayHelpers.AddUnique(ref array, element, out var index);

		// Assert
		Assert.NotNull(array);
		Assert.Equal(2,       array.Length);
		Assert.Equal(element, array[1]);
		Assert.Equal(1,       index);
	}

	[Fact]
	public void AddUnique_WhenArrayIsFull_ResizesArrayAndReturnsNewElementIndex()
	{
		// Arrange
		string?[]     array         = { "Element1", "Element2" };
		var           initialLength = array.Length;
		const string? elementToAdd  = "Element3";

		// Act
		Common.Helpers.ArrayHelpers.AddUnique(ref array, elementToAdd, out var index);

		// Assert
		Assert.NotNull(array);
		Assert.True(array.Length > initialLength);
		Assert.Equal(elementToAdd,  array[initialLength]);
		Assert.Equal(initialLength, index);
	}

	[Fact]
	public void AddUnique_WhenArrayIsNotEmpty_AddsElementAndReturnsCorrectIndex()
	{
		// Arrange
		string?[] array   = { "Element1" };
		var       element = "Element2";

		// Act
		Common.Helpers.ArrayHelpers.AddUnique(ref array, element, out var index);

		// Assert
		Assert.NotNull(array);
		Assert.Equal(2,       array.Length);
		Assert.Equal(element, array[1]);
		Assert.Equal(1,       index);
	}

	[Fact]
	public void AddUnique_WhenArrayIsNull_InitializesArrayWithElementAndReturnsZeroIndex()
	{
		// Arrange
		string?[]     array   = null;
		const string? element = "Element1";

		// Act
		Common.Helpers.ArrayHelpers.AddUnique(ref array, element, out var index);

		// Assert
		Assert.NotNull(array);
		Assert.Single(array);
		Assert.Equal(element, array[0]);
		Assert.Equal(0,       index);
	}

	[Fact]
	public void AddUnique_WhenElementIsDuplicate_DoesNotModifyArrayAndReturnsNegativeIndex()
	{
		// Arrange
		string?[] array   = { "Element1" };
		var       element = "Element1";

		// Act
		Common.Helpers.ArrayHelpers.AddUnique(ref array, element, out var index);

		// Assert
		Assert.Single(array);
		Assert.Equal(-1, index);
	}

	[Fact]
	public void AddUnique_WhenElementIsNull_DoesNotModifyArrayAndReturnsNegativeIndex()
	{
		// Arrange
		string[] array = { "Element1" };

		// Act
		Common.Helpers.ArrayHelpers.AddUnique(ref array, null, out var index);

		// Assert
		Assert.Single(array);
		Assert.Equal(-1, index);
	}
}
