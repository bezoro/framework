using System;
using Bezoro.Core.Extensions;
using Bezoro.Core.Types.Exceptions;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Common.Extensions.Collections.Search;

[TestSubject(typeof(CollectionExtensions))]
public static class CollectionExtensionsSearchTests
{
	public class Unit
	{
		[Fact]
		public void Find_EmptyCollection_ThrowsEmptyCollectionException()
		{
			int[] collection = [];
			var   action     = () => collection.Find(1);
			action.Should().Throw<EmptyCollectionException>();
		}

		[Fact]
		public void Find_ExistingItem_ReturnsItem()
		{
			int[] collection = [1, 2, 3];
			collection.Find(2).Should().Be(2);
		}

		[Fact]
		public void Find_NonExistingItem_ReturnsDefaultValue()
		{
			int[] collection = [1, 2, 3];
			collection.Find(4).Should().Be(0);
		}

		[Fact]
		public void Find_NullCollection_ThrowsArgumentNullException()
		{
			int[] collection = null!;
			var   action     = () => collection.Find(1);
			action.Should().Throw<ArgumentNullException>();
		}
	}
}
