using System;
using Bezoro.Core.Collections.Array;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace Bezoro.Core.Tests.Array
{
	[TestFixture]
	[TestOf(typeof(ArrayHelpers))]
	public class RemoveDuplicates
	{
		[Test]
		public void WhenArrayContainsCustomObjectDuplicates_UniqueInstancesAreRetained()
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

		[TestCase(new object[] { 1, 1, 1, 1 },              new object[] { 1 })]
		[TestCase(new object[] { 1, 2, 3, 3, 4, 5 },        new object[] { 1, 2, 3, 4, 5 })]
		[TestCase(new object[] { "A", "A", "B", "B", "C" }, new object[] { "A", "B", "C" })]
		public void WhenArrayContainsIntegerOrStringDuplicates_UniqueValuesAreRetained(
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
		public void WhenArrayContainsNumericOrCharDuplicates_UniqueValuesAreRetained(
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
		public void WhenArrayContainsObjectDuplicatesAndNulls_UniqueValuesAreRetainedAndNullsAreRemoved(
			object[] array,
			object[] expected)
		{
			// Act
			ArrayHelpers.RemoveDuplicates(ref array);

			// Assert
			CollectionAssert.AreEqual(expected, array);
		}

		[Test]
		public void WhenArrayContainsSingleElement_ItRemainsUnchanged()
		{
			// Arrange
			var array = new[] { 42 };

			// Act
			ArrayHelpers.RemoveDuplicates(ref array);

			// Assert
			CollectionAssert.AreEqual(new[] { 42 }, array);
		}

		[Test]
		public void WhenArrayIsEmpty_ItRemainsUnchanged()
		{
			// Arrange
			var array = System.Array.Empty<int>();

			// Act
			ArrayHelpers.RemoveDuplicates(ref array);

			// Assert
			CollectionAssert.IsEmpty(array);
		}

		[Test]
		public void WhenInputArrayIsNull_ItRemainsNull()
		{
			// Arrange
			int[] array = null;

			// Act
			ArrayHelpers.RemoveDuplicates(ref array);

			// Assert
			Assert.That(array, Is.Null);
		}

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
	}
}
