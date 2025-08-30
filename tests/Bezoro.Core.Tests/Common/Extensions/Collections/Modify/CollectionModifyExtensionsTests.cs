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
	}
}
