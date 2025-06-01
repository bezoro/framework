using System;
using System.Linq;
using Bezoro.Core.Collections.Array;
using NUnit.Framework;

namespace Bezoro.Core.Tests.Array
{
	[TestFixture]
	[TestOf(typeof(ArrayHelpers))]
	public class RemoveElementTests
	{
		[Test]
		public void RemoveElement_Sequential_InvalidRemovalApproach_ThrowsArgumentOutOfRangeException()
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
					ArrayHelpers.RemoveElement(ref array, elementToRemove, invalidApproach)
			);
		}

		[Test]
		public void WhenArrayContainsDuplicateElements_FirstOccurrenceIsRemoved()
		{
			// Arrange
			var       array           = new[] { 1, 2, 3, 3, 4, 5 };
			const int elementToRemove = 3;

			// Act
			ArrayHelpers.RemoveElement(ref array, elementToRemove);

			// Assert

			Assert.That(
				array, Is.EqualTo(new[] { 1, 2, 3, 4, 5 }),
				"The array content should match the expected sequence."
			);

			Assert.That(
				array.Length, Is.EqualTo(5),
				"The array length should be reduced by one."
			);
		}

		[Test]
		public void WhenArrayIsEmpty_ItRemainsUnchanged()
		{
			// Arrange
			var       array           = new int[0];
			const int elementToRemove = 3;

			// Act
			ArrayHelpers.RemoveElement(ref array, elementToRemove);

			// Assert
			Assert.That(array, Is.Empty, "The array should remain empty.");
		}

		[Test]
		public void WhenArrayIsNull_ItRemainsNull()
		{
			// Arrange
			int[]     array           = null;
			const int elementToRemove = 3;

			// Act
			ArrayHelpers.RemoveElement(ref array, elementToRemove);

			// Assert
			Assert.That(array, Is.Null, "The array should remain null.");
		}

		[Test]
		public void WhenArraySizeExceedsThreshold_TargetElementIsRemoved()
		{
			// Arrange
			var parallelThreshold = ArrayHelpers.ParallelThreshold;
			var array             = Enumerable.Range(1, parallelThreshold * 2).ToArray();
			var elementToRemove   = parallelThreshold;

			// Act
			ArrayHelpers.RemoveElement(ref array, elementToRemove);

			// Assert
			Assert.That(array.Contains(elementToRemove), Is.False, "The element should be removed.");
		}

		[Test]
		public void WhenArraySizeIsBelowThreshold_TargetElementIsRemoved()
		{
			// Arrange
			var       array           = new[] { 1, 2, 3, 4, 5 };
			const int elementToRemove = 3;

			// Act
			ArrayHelpers.RemoveElement(ref array, elementToRemove);

			// Assert
			Assert.That(array.Contains(elementToRemove), Is.False, "The element should be removed.");
		}

		[Test]
		public void WhenElementNotFoundInArray_ArrayRemainsUnchanged()
		{
			// Arrange
			var       array             = new[] { 1, 2, 3, 4, 5 };
			const int elementNotInArray = 6;
			var       initialLength     = array.Length;

			// Act
			ArrayHelpers.RemoveElement(ref array, elementNotInArray);

			// Assert
			Assert.That(
				array, Is.EqualTo(new[] { 1, 2, 3, 4, 5 }),
				"The array content should remain unchanged when the element is not found."
			);

			Assert.That(
				array.Length, Is.EqualTo(initialLength),
				"The array length should remain unchanged when the element is not found."
			);
		}

		[Test]
		public void WhenElementToRemoveIsNull_ArrayRemainsUnchanged()
		{
			// Arrange
			var array         = new int?[] { 1, 2, 3, 4, 5 };
			var initialLength = array.Length;

			// Act
			ArrayHelpers.RemoveElement(ref array, null);

			// Assert
			Assert.That(
				array, Is.EqualTo(new int?[] { 1, 2, 3, 4, 5 }),
				"The array content should remain unchanged when the element is null."
			);

			Assert.That(
				array.Length, Is.EqualTo(initialLength),
				"The array length should remain unchanged when the element is null."
			);
		}

		[Test]
		public void WhenRemovalApproachIsResize_ElementIsRemovedAndArrayIsResized()
		{
			// Arrange
			var       array           = new[] { 1, 2, 3, 4, 5 };
			const int elementToRemove = 3;

			// Act
			ArrayHelpers.RemoveElement(ref array, elementToRemove); // Default is Resize

			// Assert
			Assert.That(
				array, Is.EqualTo(new[] { 1, 2, 4, 5 }),
				"The array should be resized and updated after element removal."
			);

			Assert.That(
				array.Length, Is.EqualTo(4),
				"The array length should reflect the resized content."
			);
		}

		[Test]
		public void WhenRemovalApproachIsSetToNull_ElementIsNulledAndArrayLengthUnchanged()
		{
			// Arrange
			var       array           = new int?[] { 1, 2, 3, 4, 5 };
			var       initialLength   = array.Length;
			const int elementToRemove = 3;

			// Act
			ArrayHelpers.RemoveElement(ref array, elementToRemove, Enums.SetToNull);

			// Assert
			Assert.That(
				array, Is.EqualTo(new int?[] { 1, 2, null, 4, 5 }),
				"The element should be nullified in the array."
			);

			Assert.That(
				array.Length, Is.EqualTo(initialLength),
				"The length of the array should not change when nullifying an element."
			);
		}
	}
}
