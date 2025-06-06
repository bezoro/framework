using Bezoro.Core.Collections.Array;
using NUnit.Framework;

namespace Bezoro.Core.UnitTests.Collections.Array;

[TestFixture]
[TestOf(typeof(ArrayHelpers))]
public class SetupArrayWithFirstElement
{
#region Test Methods

	[Test]
	public void WhenArrayIsEmpty_InitializesWithGivenElementAndReturnsTrue()
	{
		// Arrange
		var array   = new int[0];
		var element = 42;

		// Act
		var result = ArrayHelpers.SetupArrayWithFirstElement(ref array, element);

		// Assert
		Assert.That(
			result, Is.True, "Expected the method to return true when reinitializing the array."
		);

		Assert.That(array, Is.Not.Null, "Expected the array to be initialized.");

		Assert.That(
			array.Length, Is.EqualTo(1), "Array length should be exactly 1 after initialization."
		);

		Assert.That(
			array[0], Is.EqualTo(element), "First element of the array should match the provided element."
		);
	}

	[Test]
	public void WhenArrayIsNotEmpty_ItRemainsUnchangedAndReturnsFalse()
	{
		// Arrange
		int[] array   = { 10, 20, 30 };
		var   element = 42;

		// Act
		var result = ArrayHelpers.SetupArrayWithFirstElement(ref array, element);

		// Assert
		Assert.That(
			result, Is.False, "Expected the method to return false when the array is not empty."
		);

		Assert.That(array.Length, Is.EqualTo(3),  "Array length should remain unchanged.");
		Assert.That(array[0],     Is.EqualTo(10), "First element should remain unchanged.");
		Assert.That(array[1],     Is.EqualTo(20), "Second element should remain unchanged.");
		Assert.That(array[2],     Is.EqualTo(30), "Third element should remain unchanged.");
	}

	[Test]
	public void WhenArrayIsNotEmpty_ReturnsFalse()
	{
		// Arrange
		int[] array   = { 10, 20 };
		var   element = 42;

		// Act
		var result = ArrayHelpers.SetupArrayWithFirstElement(ref array, element);

		// Assert
		Assert.That(
			result, Is.False, "Expected the method to return false when the array is already initialized."
		);
	}

	[Test]
	public void WhenArrayIsNull_InitializesWithGivenElementAndReturnsTrue()
	{
		// Arrange
		int[] array   = null;
		var   element = 42;

		// Act
		var result = ArrayHelpers.SetupArrayWithFirstElement(ref array, element);

		// Assert
		Assert.That(result, Is.True, "Expected the method to return true when initializing the array.");

		Assert.That(array, Is.Not.Null, "Expected the array to be initialized.");

		Assert.That(
			array.Length, Is.EqualTo(1), "Array length should be exactly 1 after initialization."
		);

		Assert.That(
			array[0], Is.EqualTo(element), "First element of the array should match the provided element."
		);
	}

#endregion
}
