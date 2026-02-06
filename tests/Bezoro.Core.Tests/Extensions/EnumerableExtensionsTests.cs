using System;
using System.Collections;
using System.Collections.Generic;
using Bezoro.Core.Extensions;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Extensions;

[TestSubject(typeof(EnumerableExtensions))]
public class EnumerableExtensionsTests
{
	[Fact]
	public void HasAny_NonGeneric_Should_HandleString_EmptyAndNonEmpty()
	{
		// Arrange
		IEnumerable empty    = "";
		IEnumerable nonEmpty = "abc";

		// Act & Assert
		empty.HasAny().Should().BeFalse();
		nonEmpty.HasAny().Should().BeTrue();
	}

	[Fact]
	public void HasAny_NonGeneric_Should_ReturnFalse_ForNonCollectionEmptyEnumerable()
	{
		// Arrange
		var sequence = NonGenericEnumerableEmpty();

		// Act & Assert
		sequence.HasAny().Should().BeFalse();
		return;

		static IEnumerable NonGenericEnumerableEmpty()
		{
			yield break;
		}
	}

	[Fact]
	public void HasAny_NonGeneric_Should_ReturnFalse_WhenArrayListIsEmpty()
	{
		// Arrange
		IEnumerable collection = new ArrayList();

		// Act & Assert
		collection.HasAny().Should().BeFalse();
	}

	[Fact]
	public void HasAny_NonGeneric_Should_ReturnTrue_ForNonCollectionEnumerable()
	{
		// Arrange
		var sequence = NonGenericEnumerableWithItems();

		// Act & Assert
		sequence.HasAny().Should().BeTrue();
		return;

		static IEnumerable NonGenericEnumerableWithItems()
		{
			yield return 42;
		}
	}

	[Fact]
	public void HasAny_NonGeneric_Should_ReturnTrue_WhenCollectionHasElements()
	{
		// Arrange
		ArrayList collection = new() { 1, 2, 3 };

		// Act & Assert
		collection.HasAny().Should().BeTrue();
	}

	[Fact]
	public void HasAny_Should_ReturnFalse_WhenCollectionIsEmpty()
	{
		// Arrange
		int[] collection = [];

		// Act & Assert
		collection.HasAny().Should().BeFalse();
	}

	[Fact]
	public void HasAny_Should_ReturnTrue_WhenCollectionHasElements()
	{
		// Arrange
		int[] collection = [1, 2, 3];

		// Act & Assert
		collection.HasAny().Should().BeTrue();
	}

	[Fact]
	public void HasAny_Should_ThrowArgumentNullException_WhenSourceIsNull()
	{
		// Arrange
		IEnumerable<int>? collection = null;

		// Act
		Action act = () => _ = collection!.HasAny();

		// Assert
		act.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public void IsNullOrEmpty_NonGeneric_Should_ReturnTrue_WhenNull()
	{
		// Arrange
		IEnumerable? collection = null;

		// Act & Assert
		collection.IsNullOrEmpty().Should().BeTrue();
	}

	[Fact]
	public void IsNullOrEmpty_NonGeneric_Should_Work_ForArrayList()
	{
		// Arrange
		IEnumerable empty    = new ArrayList();
		IEnumerable nonEmpty = new ArrayList { 1 };

		// Act & Assert
		empty.IsNullOrEmpty().Should().BeTrue();
		nonEmpty.IsNullOrEmpty().Should().BeFalse();
	}

	[Fact]
	public void IsNullOrEmpty_Should_ReturnFalse_WhenCollectionHasElements()
	{
		// Arrange
		int[] collection = [1, 2, 3];

		// Act & Assert
		collection.IsNullOrEmpty().Should().BeFalse();
	}

	[Fact]
	public void IsNullOrEmpty_Should_ReturnTrue_WhenCollectionIsEmpty()
	{
		// Arrange
		int[] collection = [];

		// Act & Assert
		collection.IsNullOrEmpty().Should().BeTrue();
	}

	[Fact]
	public void IsNullOrEmpty_Should_ReturnTrue_WhenCollectionIsNull()
	{
		// Arrange
		IEnumerable<int>? collection = null;

		// Act & Assert
		collection.IsNullOrEmpty().Should().BeTrue();
	}

	[Fact]
	public void PrettyJoin_Should_JoinElements_WithCustomSeparator()
	{
		// Arrange
		int[] collection = [1, 2, 3];

		// Act
		string result = collection.PrettyJoin("|");

		// Assert
		result.Should().Be("1|2|3");
	}

	[Fact]
	public void PrettyJoin_Should_JoinElements_WithDefaultSeparator()
	{
		// Arrange
		int[] collection = [1, 2, 3];

		// Act
		string result = collection.PrettyJoin();

		// Assert
		result.Should().Be("1, 2, 3");
	}

	[Fact]
	public void PrettyJoin_Should_ThrowArgumentNullException_WhenSourceIsNull()
	{
		// Arrange
		IEnumerable<int>? collection = null;

		// Act
		Action act = () => _ = collection!.PrettyJoin();

		// Assert
		act.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public void TryGetCount_Generic_Should_ReturnTrueAndCount_ForIReadOnlyCollectionOnly()
	{
		// Arrange
		IEnumerable<int> collection = new ReadOnlyCollectionStub(5, 6);

		// Act
		bool result = collection.TryGetCount(out int count);

		// Assert
		result.Should().BeTrue();
		count.Should().Be(2);
	}

	[Fact]
	public void TryGetCount_Generic_Should_ReturnTrueAndLength_ForStringWithCharType()
	{
		// Arrange
		var               str   = "test";
		IEnumerable<char> chars = str;

		// Act
		bool result = chars.TryGetCount(out int count);

		// Assert
		result.Should().BeTrue();
		count.Should().Be(4);
	}

	[Fact]
	public void TryGetCount_Generic_Should_Use_NonGenericICollection_Count_WhenAvailable()
	{
		// Arrange
		IEnumerable<int> collection = new HybridCollection(1, 2, 3);

		// Act
		bool result = collection.TryGetCount(out int count);

		// Assert
		result.Should().BeTrue();
		count.Should().Be(3);
	}

	[Fact]
	public void TryGetCount_NonGeneric_Should_ReturnFalseAndMinusOne_ForNonCollection()
	{
		// Arrange
		var enumerable = NonGenericEnumerable();

		// Act
		bool result = enumerable.TryGetCount(out int count);

		// Assert
		result.Should().BeFalse();
		count.Should().Be(-1);
		return;

		static IEnumerable NonGenericEnumerable()
		{
			yield return 1;
			yield return 2;
		}
	}

	[Fact]
	public void TryGetCount_NonGeneric_Should_ReturnTrueAndCount_ForICollection()
	{
		// Arrange
		IEnumerable collection = new ArrayList { 1, 2, 3 };

		// Act
		bool result = collection.TryGetCount(out int count);

		// Assert
		result.Should().BeTrue();
		count.Should().Be(3);
	}

	[Fact]
	public void TryGetCount_NonGeneric_Should_ReturnTrueAndLength_ForString_TypedAsEnumerable()
	{
		// Arrange
		IEnumerable str = "test";

		// Act
		bool result = str.TryGetCount(out int count);

		// Assert
		result.Should().BeTrue();
		count.Should().Be(4);
	}

	[Fact]
	public void TryGetCount_Should_ReturnFalseAndMinusOne_ForNonCollection()
	{
		// Arrange
		var enumerable = NonCollection();

		// Act
		bool result = enumerable.TryGetCount(out int count);

		// Assert
		result.Should().BeFalse();
		count.Should().Be(-1);
		return;

		static IEnumerable<int> NonCollection()
		{
			yield return 1;
			yield return 2;
			yield return 3;
		}
	}

	[Fact]
	public void TryGetCount_Should_ReturnTrueAndCount_ForCollection()
	{
		// Arrange
		var collection = new List<int> { 1, 2, 3 };

		// Act
		bool result = collection.TryGetCount(out int count);

		// Assert
		result.Should().BeTrue();
		count.Should().Be(3);
	}

	[Fact]
	public void TryGetCount_Should_ReturnTrueAndCount_ForReadOnlyCollection()
	{
		// Arrange
		IReadOnlyCollection<int> collection = new List<int> { 1, 2, 3 }.AsReadOnly();

		// Act
		bool result = collection.TryGetCount(out int count);

		// Assert
		result.Should().BeTrue();
		count.Should().Be(3);
	}

	[Fact]
	public void TryGetCount_Should_ReturnTrueAndLength_ForString()
	{
		// Arrange
		var str = "test";

		// Act
		bool result = str.TryGetCount(out int count);

		// Assert
		result.Should().BeTrue();
		count.Should().Be(4);
	}
}

internal sealed class HybridCollection(params int[] items) : ICollection, IEnumerable<int>
{
	private readonly ArrayList _list = new(items ?? Array.Empty<int>());

	public bool IsSynchronized => _list.IsSynchronized;

	public int Count => _list.Count;

	public object SyncRoot => _list.SyncRoot;

	public IEnumerator GetEnumerator() => _list.GetEnumerator();

	public void CopyTo(Array array, int index) => _list.CopyTo(array, index);

	IEnumerator<int> IEnumerable<int>.GetEnumerator()
	{
		foreach (int i in _list)
			yield return i;
	}
}

internal sealed class ReadOnlyCollectionStub(params int[] data) : IReadOnlyCollection<int>
{
	private readonly int[] _data = data ?? Array.Empty<int>();

	public int Count => _data.Length;

	public IEnumerator<int> GetEnumerator() => ((IEnumerable<int>)_data).GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => _data.GetEnumerator();
}
