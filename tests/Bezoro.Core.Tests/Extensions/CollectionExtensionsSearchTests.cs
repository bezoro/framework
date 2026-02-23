using System;
using Bezoro.Core.Extensions;
using Bezoro.Core.Types.Exceptions;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Extensions;

[TestSubject(typeof(CollectionExtensions))]
public class CollectionExtensionsSearchTests
{
	[Fact]
	public void FindEmptyCollection_WhenCalled_ShouldThrowEmptyCollectionException()
	{
		int[] collection = [];
		var   action     = () => collection.Find(1);
		action.Should().Throw<EmptyCollectionException>();
	}

	[Fact]
	public void FindExistingItem_WhenCalled_ShouldReturnItem()
	{
		int[] collection = [1, 2, 3];
		collection.Find(2).Should().Be(2);
	}

	[Fact]
	public void FindNonExistingItem_WhenCalled_ShouldReturnDefaultValue()
	{
		int[] collection = [1, 2, 3];
		collection.Find(4).Should().Be(0);
	}

	[Fact]
	public void FindNullCollection_WhenCalled_ShouldThrowArgumentNullException()
	{
		int[] collection = null!;
		var   action     = () => collection.Find(1);
		action.Should().Throw<ArgumentNullException>();
	}
}
