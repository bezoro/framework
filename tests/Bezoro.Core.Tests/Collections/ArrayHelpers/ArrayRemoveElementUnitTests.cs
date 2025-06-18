using System;
using System.Linq;
using Xunit;
using Array = System.Array;

namespace Bezoro.Core.Tests.Collections.ArrayHelpers;

public class ArrayRemoveElementUnitTests
{
	[Fact]
	public void RemoveElement_WhenArrayContainsDuplicateElements_ThenFirstOccurrenceIsRemoved()
	{
		// Arrange
		var       array           = new[] { 1, 2, 3, 3, 4, 5 };
		const int elementToRemove = 3;

		// Act
		Common.Helpers.ArrayHelpers.RemoveElement(ref array, elementToRemove);

		// Assert
		Assert.Equal(new[] { 1, 2, 3, 4, 5 }, array);
		Assert.Equal(5,                       array.Length);
	}

	[Fact]
	public void RemoveElement_WhenArrayIsEmpty_ThenItRemainsUnchanged()
	{
		// Arrange
		int[]     array           = Array.Empty<int>();
		const int elementToRemove = 3;

		// Act
		Common.Helpers.ArrayHelpers.RemoveElement(ref array, elementToRemove);

		// Assert
		Assert.Empty(array);
	}

	[Fact]
	public void RemoveElement_WhenArrayIsNull_ThenItRemainsNull()
	{
		// Arrange
		int[]?    array           = null;
		const int elementToRemove = 3;

		// Act
		Common.Helpers.ArrayHelpers.RemoveElement(ref array, elementToRemove);

		// Assert
		Assert.Null(array);
	}

	[Fact]
	public void RemoveElement_WhenArraySizeExceedsThreshold_ThenTargetElementIsRemoved()
	{
		// Arrange
		int   parallelThreshold = Common.Helpers.ArrayHelpers.ParallelThreshold;
		int[] array             = Enumerable.Range(1, parallelThreshold * 2).ToArray();
		int   elementToRemove   = parallelThreshold;

		// Act
		Common.Helpers.ArrayHelpers.RemoveElement(ref array, elementToRemove);

		// Assert
		Assert.DoesNotContain(elementToRemove, array);
	}

	[Fact]
	public void RemoveElement_WhenArraySizeIsBelowThreshold_ThenTargetElementIsRemoved()
	{
		// Arrange
		var       array           = new[] { 1, 2, 3, 4, 5 };
		const int elementToRemove = 3;

		// Act
		Common.Helpers.ArrayHelpers.RemoveElement(ref array, elementToRemove);

		// Assert
		Assert.DoesNotContain(elementToRemove, array);
	}

	[Fact]
	public void RemoveElement_WhenElementNotFoundInArray_ThenArrayRemainsUnchanged()
	{
		// Arrange
		var       array             = new[] { 1, 2, 3, 4, 5 };
		const int elementNotInArray = 6;
		int       initialLength     = array.Length;

		// Act
		Common.Helpers.ArrayHelpers.RemoveElement(ref array, elementNotInArray);

		// Assert
		Assert.Equal(new[] { 1, 2, 3, 4, 5 }, array);
		Assert.Equal(initialLength,           array.Length);
	}

	[Fact]
	public void RemoveElement_WhenElementToRemoveIsNull_ThenArrayRemainsUnchanged()
	{
		// Arrange
		var array         = new int?[] { 1, 2, 3, 4, 5 };
		int initialLength = array.Length;

		// Act
		Common.Helpers.ArrayHelpers.RemoveElement(ref array, null);

		// Assert
		Assert.Equal(new int?[] { 1, 2, 3, 4, 5 }, array);
		Assert.Equal(initialLength,                array.Length);
	}

	[Fact]
	public void RemoveElement_WhenRemovalApproachIsInvalid_ThenThrowsArgumentOutOfRangeException()
	{
		// Arrange
		var array           = new[] { 10, 20, 30 }; // Small array to ensure sequential path
		var elementToRemove = 20;

		// Use an integer value that is not a defined member of the Enums for removalApproach
		// This simulates an invalid enum value being passed.
		var invalidApproach = (Enums)int.MaxValue;

		// Act & Assert
		// We expect an ArgumentOutOfRangeException when the default case in the switch statement is hit.
		Assert.Throws<ArgumentOutOfRangeException>(
			() =>
				Common.Helpers.ArrayHelpers.RemoveElement(ref array, elementToRemove, invalidApproach)
		);
	}

	[Fact]
	public void RemoveElement_WhenRemovalApproachIsResize_ThenElementIsRemovedAndArrayIsResized()
	{
		// Arrange
		var       array           = new[] { 1, 2, 3, 4, 5 };
		const int elementToRemove = 3;

		// Act
		Common.Helpers.ArrayHelpers.RemoveElement(ref array, elementToRemove); // Default is Resize

		// Assert
		Assert.Equal(new[] { 1, 2, 4, 5 }, array);
		Assert.Equal(4,                    array.Length);
	}

	[Fact]
	public void RemoveElement_WhenRemovalApproachIsSetToNull_ThenElementIsNulledAndArrayLengthUnchanged()
	{
		// Arrange
		var       array           = new int?[] { 1, 2, 3, 4, 5 };
		int       initialLength   = array.Length;
		const int elementToRemove = 3;

		// Act
		Common.Helpers.ArrayHelpers.RemoveElement(ref array, elementToRemove, Enums.SetToNull);

		// Assert
		Assert.Equal(new int?[] { 1, 2, null, 4, 5 }, array);
		Assert.Equal(initialLength,                   array.Length);
	}
}
