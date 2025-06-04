using System;
using Bezoro.Core.Collections.Array;
using NUnit.Framework;

namespace Bezoro.Core.Tests.Array
{
	[TestFixture]
	public class Merge
	{
	#region Test Methods

		[Test]
		public void BothSourceAndDestinationArraysAreNull_DestinationRemainsNull()
		{
			// Arrange
			string[] to = null;

			// Act
			ArrayHelpers.Merge(null, ref to);

			// Assert
			Assert.That(to, Is.Null);
		}

		[Test]
		public void CustomObjectArrayAndDestinationSmaller_DestinationResizedAndAllElementsCopied()
		{
			// Arrange
			var from = new[]
			{
				new TestObject(1, "Object1"),
				new TestObject(2, "Object2"),
				new TestObject(3, "Object3")
			};

			var to = new TestObject[2]; // Smaller array than 'from'

			// Act
			ArrayHelpers.Merge(from, ref to);

			// Assert
			Assert.That(to, Is.EqualTo(from));
		}

		[Test]
		public void DestinationArrayContainsPartialNulls_NullsInDestinationReplacedByCorrespondingSourceElements()
		{
			// Arrange
			var from = new[] { "A", "B", "C" };
			var to   = new[] { "X", null, "Z" };

			// Act
			ArrayHelpers.Merge(from, ref to);

			// Assert
			Assert.That(to, Is.EqualTo(new[] { "X", "B", "Z" }));
		}

		[Test]
		public void DestinationArrayHasExistingDataAndNulls_OnlyNullsInDestinationReplacedBySourceElements()
		{
			// Arrange
			var from = new[] { "X", "Y", "Z" };
			var to   = new[] { "A", "B", null };

			// Act
			ArrayHelpers.Merge(from, ref to);

			// Assert
			Assert.That(to, Is.EqualTo(new[] { "A", "B", "Z" }));
		}

		[Test]
		public void DestinationArrayIsEmpty_DestinationResizedAndAllSourceElementsCopied()
		{
			// Arrange
			var from = new[] { "A", "B", "C" };
			var to   = System.Array.Empty<string>();

			// Act
			ArrayHelpers.Merge(from, ref to);

			// Assert
			Assert.That(to, Is.EqualTo(from));
		}

		[Test]
		public void DestinationArrayIsNull_DestinationInitializedAndAllSourceElementsCopied()
		{
			// Arrange
			var      from = new[] { "A", "B", "C" };
			string[] to   = null;

			// Act
			ArrayHelpers.Merge(from, ref to);

			// Assert
			Assert.That(to, Is.EqualTo(from));
		}

		[Test]
		public void DestinationArrayLargerAndContainsElements_DestinationArrayUnchanged()
		{
			// Arrange
			var from = new[] { 1, 2 };
			var to   = new[] { 3, 4, 5 };

			// Act
			ArrayHelpers.Merge(from, ref to);

			// Assert
			Assert.That(to, Is.EqualTo(new[] { 3, 4, 5 }));
		}

		[Test]
		public void SourceArrayIsEmpty_DestinationArrayUnchanged()
		{
			// Arrange
			var from = System.Array.Empty<string>();
			var to   = new[] { "Existing" };

			// Act
			ArrayHelpers.Merge(from, ref to);

			// Assert
			Assert.That(to, Is.EqualTo(new[] { "Existing" }));
		}

		[Test]
		public void SourceArrayIsNull_DestinationArrayUnchanged()
		{
			// Arrange
			string[] to = { "Existing" };

			// Act
			ArrayHelpers.Merge(null, ref to);

			// Assert
			Assert.That(to, Is.EqualTo(new[] { "Existing" }));
		}

		[Test]
		public void SourceArrayLargerThanDestination_DestinationResizedAndAllElementsCopied()
		{
			// Arrange
			var from = new[] { 1, 2, 3 };
			var to   = new int[2]; // Smaller array than 'from'

			// Act
			ArrayHelpers.Merge(from, ref to);

			// Assert
			Assert.That(to, Is.EqualTo(new[] { 1, 2, 3 }));
		}

		[Test]
		public void SourceArrayLargerThanDestination_DestinationResizedToFitSource()
		{
			// Arrange
			var from = new[] { 100, 200, 300 };
			var to   = new int[1]; // Smaller array than 'from'

			// Act
			ArrayHelpers.Merge(from, ref to);

			// Assert
			Assert.That(to, Is.EqualTo(from));
		}

	#endregion

	#region Helper Methods/Other Members

		// Test class for complex type checking
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
				{
					return Id == other.Id && Name == other.Name;
				}

				return false;
			}

			public override int GetHashCode() =>
				HashCode.Combine(Id, Name);
		}

	#endregion
	}
}
