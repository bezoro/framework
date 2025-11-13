using System;
using System.Collections.Generic;
using Bezoro.Core.Common.Extensions.Collections.Modify;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Common.Extensions.Collections.Modify;

[TestSubject(typeof(CollectionModifyExtensions))]
public static class CollectionModifyExtensionsTests
{
	public class Unit
	{
		[Fact]
		public void AddRange_WhenValidCollection_ShouldAddItemsToCollection()
		{
			// Arrange
			ICollection<int> sourceCollection = new List<int> { 1, 2, 3 };
			ICollection<int> itemsToAdd       = new List<int> { 4, 5, 6 };

			// Act
			sourceCollection.AddRange(itemsToAdd);

			// Assert
			sourceCollection.Should().HaveCount(6);
			sourceCollection.Should().ContainInOrder(1, 2, 3, 4, 5, 6);
		}

		[Fact]
		public void AddRange_WhenTargetCollectionEmpty_ShouldPopulateWithItems()
		{
			// Arrange
			ICollection<string> target = new List<string>();
			ICollection<string> items  = new List<string> { "foo", "bar" };

			// Act
			target.AddRange(items);

			// Assert
			target.Should().ContainInOrder("foo", "bar");
		}

		[Fact]
		public void AddRange_WhenItemsCollectionEmpty_ShouldDoNothing()
		{
			// Arrange
			ICollection<int> target = new List<int> { 42 };
			ICollection<int> items  = new List<int>();

			// Act
			target.AddRange(items);

			// Assert
			target.Should().Equal(42);
		}

		[Fact]
		public void AddRange_WhenCollectionNull_ShouldThrow()
		{
			// Arrange
			ICollection<int>? target = null;
			ICollection<int>  items  = new List<int> { 1 };

			// Act
			var act = () => target!.AddRange(items);

			// Assert
			act.Should().Throw<ArgumentNullException>();
		}

		[Fact]
		public void AddRange_WhenItemsNull_ShouldThrow()
		{
			// Arrange
			ICollection<int> target = new List<int> { 1 };
			ICollection<int>? items = null;

			// Act
			var act = () => target.AddRange(items!);

			// Assert
			act.Should().Throw<ArgumentNullException>();
		}
	}
}
