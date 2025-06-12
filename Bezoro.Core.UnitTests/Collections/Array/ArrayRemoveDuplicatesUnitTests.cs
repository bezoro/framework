using System;
using Bezoro.Core.Collections.Array;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace Bezoro.Core.UnitTests.Collections.Array;

[TestFixture]
[TestOf(typeof(ArrayHelpers))]
public class ArrayRemoveDuplicatesUnitTests
{
#region Test Methods

	[Test]
	public void RemoveDuplicates_WhenArrayContainsCustomObjectDuplicates_ThenUniqueInstancesAreRetained()
	{
		// Arrange
		var array = new[]
		{
			new TestObject(1, "Object1"),
			new TestObject(2, "Object2"),
			new TestObject(1, "Object1"), // Duplicate
			new TestObject(3, "Object3"),
			new TestObject(2, "Object2") // Duplicate
		};

		var expectedArray = new[]
		{
			new TestObject(1, "Object1"),
			new TestObject(2, "Object2"),
			new TestObject(3, "Object3")
		};

		// Act
		ArrayHelpers.RemoveDuplicates(ref array);

		// Assert
		CollectionAssert.AreEqual(expectedArray, array);
	}

	[Test]
	public void RemoveDuplicates_WhenArrayContainsSingleElement_ThenItRemainsUnchanged()
	{
		// Arrange
		var array = new[] { 42 };

		// Act
		ArrayHelpers.RemoveDuplicates(ref array);

		// Assert
		CollectionAssert.AreEqual(new[] { 42 }, array);
	}

	[Test]
	public void RemoveDuplicates_WhenArrayIsEmpty_ThenItRemainsUnchanged()
	{
		// Arrange
		var array = System.Array.Empty<int>();

		// Act
		ArrayHelpers.RemoveDuplicates(ref array);

		// Assert
		CollectionAssert.IsEmpty(array);
	}

	[Test]
	public void RemoveDuplicates_WhenInputArrayIsNull_ThenItRemainsNull()
	{
		// Arrange
		int[] array = null;

		// Act
		ArrayHelpers.RemoveDuplicates(ref array);

		// Assert
		Assert.That(array, Is.Null);
	}

	[TestCase(new object[] { 1, 1, 1, 1 },              new object[] { 1 })]
	[TestCase(new object[] { 1, 2, 3, 3, 4, 5 },        new object[] { 1, 2, 3, 4, 5 })]
	[TestCase(new object[] { "A", "A", "B", "B", "C" }, new object[] { "A", "B", "C" })]
	public void RemoveDuplicates_WhenArrayContainsIntegerOrStringDuplicates_ThenUniqueValuesAreRetained(
		object[] array,
		object[] expectedArray)
	{
		// Act
		ArrayHelpers.RemoveDuplicates(ref array);

		// Assert
		CollectionAssert.AreEqual(expectedArray, array);
	}

	[TestCase(new object[] { 1, 1, 2, 2, 3, 3 },        new object[] { 1, 2, 3 })]
	[TestCase(new object[] { 1.1, 1.1, 2.2, 3.3, 3.3 }, new object[] { 1.1, 2.2, 3.3 })]
	[TestCase(new object[] { 'a', 'a', 'b', 'c', 'c' }, new object[] { 'a', 'b', 'c' })]
	public void RemoveDuplicates_WhenArrayContainsNumericOrCharDuplicates_ThenUniqueValuesAreRetained(
		object[] array,
		object[] expectedArray)
	{
		// Act
		ArrayHelpers.RemoveDuplicates(ref array);

		// Assert
		CollectionAssert.AreEqual(expectedArray, array);
	}

	[TestCase(new object[] { 1, 2, 2, null, 1, 3, null },      new object[] { 1, 2, 3 })]
	[TestCase(new object[] { null, null, null },               new object[] { })]
	[TestCase(new object[] { "X", "Y", "X", null, "Y", null }, new object[] { "X", "Y" })]
	public void
		RemoveDuplicates_WhenArrayContainsObjectDuplicatesAndNulls_ThenUniqueValuesAreRetainedAndNullsAreRemoved(
			object[] array,
			object[] expected)
	{
		// Act
		ArrayHelpers.RemoveDuplicates(ref array);

		// Assert
		CollectionAssert.AreEqual(expected, array);
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

		public override bool Equals(object obj)
		{
			if (obj is TestObject other)
				return Id == other.Id && Name == other.Name;

			return false;
		}

		public override int GetHashCode() =>
			HashCode.Combine(Id, Name);
	}

#endregion
}