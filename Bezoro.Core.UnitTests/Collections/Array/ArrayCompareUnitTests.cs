using Bezoro.Core.Collections.Array;
using NUnit.Framework;

namespace Bezoro.Core.UnitTests.Collections.Array;

[TestFixture]
[TestOf(typeof(ArrayHelpers))]
public class ArrayCompareUnitTests
{
#region Test Methods

	[Test]
	public void CompareArrays_WhenArraysContainEqualCustomObjects_ReturnsTrue()
	{
		// Arrange
		var obj1 = new TestObject(1, "Test1");
		var obj2 = new TestObject(2, "Test2");
		var obj3 = new TestObject(1, "Test1"); // Equal to obj1 by value

		var array1 = new[] { obj1, obj2 };
		var array2 = new[] { obj1, obj2 }; // Identical instance and values to array1
		var array3 = new[] { obj3, obj2 }; // Different instance for first element, but value-equal to array1

		// Act
		var result1 = ArrayHelpers.CompareArrays(array1, array2); // array1 vs array2 (identical)
		var result2 = ArrayHelpers.CompareArrays(array1, array3); // array1 vs array3 (value-equal)

		// Assert
		Assert.That(
			result1, Is.True, "Comparing an array to itself or an identical array should return true."
		);

		Assert.That(
			result2, Is.True, "Comparing arrays with element-wise equal custom objects should return true."
		);
	}

	[Test]
	public void CompareArrays_WhenArraysHaveDifferentLengths_ReturnsFalse()
	{
		// Arrange
		var array1 = new[] { 1, 2, 3 };
		var array2 = new[] { 1, 2, 3, 4 };

		// Act
		var result = ArrayHelpers.CompareArrays(array1, array2);

		// Assert
		Assert.That(
			result, Is.False, "Comparing 2 arrays with different lengths should be considered unequal."
		);
	}

	[Test]
	public void CompareArrays_WhenBothArraysAreNull_ReturnsTrue()
	{
		// Act
		var result = ArrayHelpers.CompareArrays<object>(null, null);

		// Assert
		Assert.That(result, Is.True, "Comparing 2 null arrays should be considered equal.");
	}

	[Test]
	public void CompareArrays_WhenBothArraysContainOnlyNullsAndSameLength_ReturnsTrue()
	{
		// Arrange
		string[] array1 = { null!, null!, null! };
		string[] array2 = { null!, null!, null! };

		// Act
		var result = ArrayHelpers.CompareArrays(array1, array2);

		// Assert
		Assert.That(
			result, Is.True,
			"Comparing 2 arrays of the same length with only null elements should be considered equal."
		);
	}

	[Test]
	public void CompareArrays_WithMixedNullElements_ReturnsTrueForIdenticalAndFalseForDifferent()
	{
		// Arrange
		string?[] array1Identical = { "a", null, "c" };
		string?[] array2Identical = { "a", null, "c" };
		string?[] array3Different = { "a", null, "b" };

		// Act
		var resultForIdentical = ArrayHelpers.CompareArrays(array1Identical, array2Identical);
		var resultForDifferent = ArrayHelpers.CompareArrays(array1Identical, array3Different);

		// Assert
		Assert.That(resultForIdentical, Is.True,  "Identical arrays with null elements should return true.");
		Assert.That(resultForDifferent, Is.False, "Different arrays with null elements should return false.");
	}

	[TestCase(
		new object[] { 1, 2, 3 }, new object[] { 1, 2, 4 },
		TestName = "CompareArrays_WhenIntegerArraysDiffer_ReturnsFalse")]
	[TestCase(
		new object[] { "a", "b", "c" }, new object[] { "a", "b", "d" },
		TestName = "CompareArrays_WhenStringArraysDiffer_ReturnsFalse")]
	[TestCase(
		new object[0], new object[] { 0 },
		TestName =
			"CompareArrays_WhenOneIsEmptyAndOtherIsNot_ReturnsFalse")] // Technically different lengths too
	public void CompareArrays_WhenArraysHaveDifferentElements_ReturnsFalse(object[]? a, object[]? b)
	{
		// Act
		var comparisonResult = ArrayHelpers.CompareArrays(a, b);

		// Assert
		Assert.That(
			comparisonResult, Is.EqualTo(false),
			"Comparing 2 arrays with different elements should be considered unequal."
		);
	}

	[TestCase(
		new object[] { 1.1, 2.2, 3.3 }, new object[] { 1.1, 2.2, 3.3 },
		TestName = "CompareArrays_WhenDoubleArraysAreEqual_ReturnsTrue")]
	[TestCase(
		new object[] { true, false }, new object[] { true, false },
		TestName = "CompareArrays_WhenBooleanArraysAreEqual_ReturnsTrue")]
	public void CompareArrays_WhenArraysOfSimpleTypesAreEqual_ReturnsTrue(object[]? a, object[]? b)
	{
		// Act
		var result = ArrayHelpers.CompareArrays(a, b);

		// Assert
		Assert.That(
			result, Is.EqualTo(true),
			"Comparing 2 arrays with the same simple type elements should be considered equal."
		);
	}

	[TestCase(
		new object[] { 1, 2, 3 }, null,
		TestName = "CompareArrays_WhenFirstArrayIsValidAndSecondIsNull_ReturnsFalse")]
	[TestCase(
		null, new object[] { 1, 2, 3 },
		TestName = "CompareArrays_WhenFirstArrayIsNullAndSecondIsValid_ReturnsFalse")]
	public void CompareArrays_WhenOneArrayIsNullAndOtherIsNot_ReturnsFalse(object[]? a, object[]? b)
	{
		// Act
		var result = ArrayHelpers.CompareArrays(a, b);

		// Assert
		Assert.That(
			result, Is.EqualTo(false), "Comparing a null array with a valid array should return false."
		);
	}

#endregion

#region Helper Methods/Other Members

	private class TestObject
	{
		public TestObject(int id, string name)
		{
			Id   = id;
			Name = name;
		}

		public int    Id   { get; }
		public string Name { get; }

		public override bool Equals(object? obj) =>
			obj is TestObject other && Id == other.Id && Name == other.Name;

		public override int GetHashCode() =>
			Id.GetHashCode() ^ Name.GetHashCode();
	}

#endregion
}