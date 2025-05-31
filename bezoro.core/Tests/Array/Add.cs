using System.Linq;
using Bezoro.Core.Collections.Array;
using NUnit.Framework;

namespace Bezoro.Core.Tests.Array
{
	[TestFixture]
	[TestOf(typeof(ArrayHelpers))]
	public class Add
	{
		[Test]
		public void WhenArrayHasEmptySlot_AddsElementToFirstEmptySlot()
		{
			// Arrange
			var array        = new[] { new TestObject(), null, new TestObject() };
			var elementToAdd = new TestObject();

			// Act
			ArrayHelpers.Add(ref array, elementToAdd);

			// Assert
			Assert.That(
				array.Contains(elementToAdd), Is.True, "Element should be added to the first empty slot."
			);

			Assert.That(
				array.Count(obj => obj == null), Is.EqualTo(0), "There should be no empty slots left."
			);
		}

		[Test]
		public void WhenArrayIsEmpty_AddsElementAndResizesArray()
		{
			// Arrange
			TestObject?[] array         = { };
			var           elementToAdd  = new TestObject();
			var           initialLength = array.Length;

			// Act
			ArrayHelpers.Add(ref array, elementToAdd);

			// Assert
			Assert.That(
				array, Is.Not.Null, "Array should not be null."
			);

			Assert.That(
				array.Length, Is.GreaterThan(0), "Array should not be empty."
			);

			Assert.That(
				array.Contains(elementToAdd), "Element should be present in the array."
			);

			Assert.That(
				array.Length, Is.GreaterThan(initialLength), "Array length should increase."
			);
		}

		[Test]
		public void WhenArrayIsNotEmptyAndHasNoEmptySlots_AddsElementAndResizesArray()
		{
			// Arrange
			TestObject?[] array         = { new TestObject(), new TestObject() };
			var           elementToAdd  = new TestObject();
			var           initialLength = array.Length;

			// Act
			ArrayHelpers.Add(ref array, elementToAdd);

			// Assert
			Assert.That(
				array, Is.Not.Null, "Array should not be null."
			);

			Assert.That(
				array.Length, Is.GreaterThan(0), "Array should not be empty."
			);

			Assert.That(
				array.Contains(elementToAdd), Is.True, "Element should be present in the array."
			);

			Assert.That(
				array.Length, Is.GreaterThan(initialLength), "Array length should increase."
			);
		}

		[Test]
		public void WhenArrayIsNull_InitializesArrayAndAddsElement()
		{
			// Arrange
			TestObject?[] array        = null;
			var           elementToAdd = new TestObject();

			// Act
			ArrayHelpers.Add(ref array, elementToAdd);

			// Assert
			Assert.That(array, Is.Not.Null, "Array should be initialized.");

			Assert.That(
				array.Length, Is.GreaterThanOrEqualTo(1), "Array should have at least 1 length."
			);

			Assert.That(
				array[0], Is.EqualTo(elementToAdd), "Element should be added to the array."
			);
		}

		[Test]
		public void WhenElementIsAdded_ReturnsCorrectIndexOfAddedElement()
		{
			// Arrange
			TestObject?[] array         = { new TestObject(), new TestObject(), new TestObject() };
			var           elementToAdd  = new TestObject();
			var           initialLength = array.Length;

			// Act
			ArrayHelpers.Add(ref array, elementToAdd, out var index);

			// Assert
			Assert.That(
				array.Contains(elementToAdd), "Element should be present in the array."
			);

			Assert.That(
				index, Is.EqualTo(initialLength), "Index should match the position of the newly added element."
			);
		}

		[Test]
		public void WhenElementIsNull_DoesNotModifyArray()
		{
			// Arrange
			var array         = new[] { new TestObject(), new TestObject() };
			var initialLength = array.Length;

			// Act
			ArrayHelpers.Add(ref array, null);

			// Assert
			Assert.That(
				array.Length, Is.EqualTo(initialLength), "Array length should remain unchanged."
			);

			Assert.That(
				array.Contains(null), Is.False, "Null element should not be added to the array."
			);
		}

		private class TestObject { }
	}
}
