using Bezoro.Core.Collections.Array;
using NUnit.Framework;

namespace Bezoro.Core.Tests.Array
{
	[TestFixture]
	[TestOf(typeof(ArrayHelpers))]
	public class AddUnique
	{
	#region Test Methods

		[Test]
		public void WhenArrayIsFullAndElementIsUnique_ResizesArrayAndAddsElement_ReturnsCorrectIndex()
		{
			// Arrange
			string?[]     array         = { "Element1", "Element2" };
			var           initialLength = array.Length;
			const string? elementToAdd  = "Element3";

			// Act
			ArrayHelpers.AddUnique(ref array, elementToAdd, out var index);

			// Assert
			Assert.That(array, Is.Not.Null, "Array should not be null after resizing.");

			Assert.That(
				array.Length, Is.GreaterThan(initialLength),
				"Array length should increase after adding an element to a full array."
			);

			Assert.That(
				array[initialLength], Is.EqualTo(elementToAdd), "New element should be added at the correct position."
			);

			Assert.That(index, Is.EqualTo(initialLength), "Index of the added element is not correct.");
		}

		[Test]
		public void WhenArrayIsNotEmptyAndElementIsUnique_AddsElementAndResizes_ReturnsCorrectIndex()
		{
			// Arrange
			string?[] array   = { "Element1" };
			var       element = "Element2";
			int       index;

			// Act
			ArrayHelpers.AddUnique(ref array, element, out index);

			// Assert
			Assert.That(array,        Is.Not.Null,         "Array should not be null after adding an element.");
			Assert.That(array.Length, Is.EqualTo(2),       "Array length should be updated correctly.");
			Assert.That(array[1],     Is.EqualTo(element), "New element should be added at the correct position.");
			Assert.That(index,        Is.EqualTo(1),       "Index of added element should be accurate.");
		}

		[Test]
		public void WhenArrayIsNull_InitializesArrayAndAddsElement_ReturnsCorrectIndex()
		{
			// Arrange
			string?[]     array   = null;
			const string? element = "Element1";
			int           index;

			// Act
			ArrayHelpers.AddUnique(ref array, element, out index);

			// Assert
			Assert.That(array,        Is.Not.Null,         "Array should be initialized.");
			Assert.That(array.Length, Is.EqualTo(1),       "Array length should be 1 after adding the element.");
			Assert.That(array[0],     Is.EqualTo(element), "The added element should match the provided value.");
			Assert.That(index,        Is.EqualTo(0),       "The index of the added element should be correct.");
		}

		[Test]
		public void WhenArrayLengthExceedsThreshold_AddsUniqueElementUsingParallelMethod_ReturnsCorrectIndex()
		{
			// Arrange
			var           array         = new string?[ArrayHelpers.ParallelThreshold * 2];
			var           initialLength = array.Length;
			const string? elementToAdd  = "Element999";

			for (var i = 0 ; i < array.Length ; i++)
				array[i] = $"Element{i + 1}";

			// Act
			ArrayHelpers.AddUnique(ref array, elementToAdd, out var index);

			// Assert
			Assert.That(array, Is.Not.Null, "Array should not be null after adding the element.");

			Assert.That(
				array, Does.Contain(elementToAdd), "The new element should be correctly added to the array."
			);

			Assert.That(index, Is.EqualTo(initialLength), "Index of the added element should be accurate.");

			Assert.That(
				array.Length, Is.GreaterThan(initialLength),
				"Array length should increase after adding a unique element."
			);
		}

		[Test]
		public void WhenArrayLengthIsBelowThreshold_AddsUniqueElementUsingSequentialMethod_ReturnsCorrectIndex()
		{
			// Arrange
			string?[]     array   = { "Element1" };
			const string? element = "Element2";

			// Act
			ArrayHelpers.AddUnique(ref array, element, out var index);

			// Assert
			Assert.That(array, Is.Not.Null, "Array should not be null after adding the element.");

			Assert.That(
				array.Length, Is.EqualTo(2), "Array length should increase after adding a unique element."
			);

			Assert.That(
				array[1], Is.EqualTo(element), "The new element should be correctly added to the array."
			);

			Assert.That(index, Is.EqualTo(1), "Index of the added element should be accurate.");
		}

		[Test]
		public void WhenElementIsDuplicate_DoesNotAddElement_ReturnsIndexNegativeOne()
		{
			// Arrange
			string?[] array   = { "Element1" };
			var       element = "Element1";
			int       index;

			// Act
			ArrayHelpers.AddUnique(ref array, element, out index);

			// Assert
			Assert.That(
				array.Length, Is.EqualTo(1), "The array length should remain unchanged for a duplicate element."
			);

			Assert.That(index, Is.EqualTo(-1), "The index should indicate that no element was added.");
		}

		[Test]
		public void WhenElementIsNull_DoesNotModifyArray_ReturnsIndexNegativeOne()
		{
			// Arrange
			string[] array = { "Element1" };

			// Act
			ArrayHelpers.AddUnique(ref array, null, out var index);

			// Assert
			Assert.That(
				array.Length, Is.EqualTo(1), "The array length should remain unchanged when the element to add is null."
			);

			Assert.That(index, Is.EqualTo(-1), "The index should indicate that no element was added.");
		}

		[Test]
		public void WhenElementIsUnique_AddsElement_And_WhenElementIsDuplicate_DoesNotAddElement()
		{
			// Arrange
			string?[]     array         = { "Element1", "Element2" };
			var           initialLength = array.Length;
			const string? element       = "Element3";

			// Act
			ArrayHelpers.AddUnique(ref array, element, out _);

			// Assert
			Assert.That(array, Is.Not.Null, "Array should not be null after adding an element.");

			Assert.That(
				array.Length, Is.GreaterThan(initialLength),
				"The array length should increase after adding a new element."
			);

			Assert.That(
				array[initialLength], Is.EqualTo(element),
				"The added unique element should be at the correct position."
			);

			// Attempt to add a duplicate element
			var newLength = array.Length;
			ArrayHelpers.AddUnique(ref array, element, out _);

			// Assert that duplicate is not added
			Assert.That(
				array.Length, Is.EqualTo(newLength), "Array length should remain unchanged for a duplicate element."
			);
		}

	#endregion
	}
}
