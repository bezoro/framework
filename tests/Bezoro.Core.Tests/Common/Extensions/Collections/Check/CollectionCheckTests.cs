using System.Collections.Generic;
using Bezoro.Core.Common.Extensions.Collections.Check;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Common.Extensions.Collections.Check;

[TestSubject(typeof(CollectionCheck))]
public static class CollectionCheckTests
{
	public class Unit
	{
		[Fact]
		public void IsEmpty_WhenEmpty_ShouldReturnTrue()
		{
			// Arrange
			var collection = new List<int>();

			// Act
			bool result = collection.IsEmpty();

			// Assert
			result.Should().BeTrue();
		}

		[Fact]
		public void IsEmpty_WhenNotEmpty_ShouldReturnFalse()
		{
			// Arrange
			var collection = new List<int> { 1 };

			// Act
			bool result = collection.IsEmpty();

			// Assert
			result.Should().BeFalse();
		}

		[Fact]
		public void IsNull_WhenNotNull_ShouldReturnFalse()
		{
			// Arrange
			var collection = new List<int>();

			// Act
			bool result = collection.IsNull();

			// Assert
			result.Should().BeFalse();
		}

		[Fact]
		public void IsNull_WhenNull_ShouldReturnTrue()
		{
			// Arrange
			List<int>? collection = null;

			// Act
			bool result = collection.IsNull();

			// Assert
			result.Should().BeTrue();
		}

		[Fact]
		public void IsNullOrEmpty_WhenNotNullOrEmpty_ShouldReturnFalse()
		{
			// Arrange
			var collection = new List<int> { 1 };

			// Act
			bool result = collection.IsNullOrEmpty();

			// Assert
			result.Should().BeFalse();
		}

		[Fact]
		public void IsNullOrEmpty_WhenNullOrEmpty_ShouldReturnTrue()
		{
			// Arrange
			var        emptyCollection = new List<int>();
			List<int>? nullCollection  = null;

			// Act
			bool result = emptyCollection.IsNullOrEmpty() && nullCollection.IsNullOrEmpty();

			// Assert
			result.Should().BeTrue();
		}

		[Fact]
		public void ThrowIfEmpty_WhenEmpty_ShouldThrowEmptyCollectionException()
		{
			// Arrange
			var collection = new List<int>();

			// Act
			var act = () => collection.ThrowIfEmpty();

			// Assert
			act.Should().Throw<EmptyCollectionException>();
		}

		[Fact]
		public void ThrowIfEmpty_WhenNotEmpty_ShouldNotThrow()
		{
			// Arrange
			var collection = new List<int> { 1 };

			// Act
			var act = () => collection.ThrowIfEmpty();

			// Assert
			act.Should().NotThrow();
		}
	}
}
