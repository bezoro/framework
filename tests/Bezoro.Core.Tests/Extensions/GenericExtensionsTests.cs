using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Bezoro.Core.Extensions;
using Bezoro.Core.Types.Exceptions;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Extensions;

[TestSubject(typeof(GenericExtensions))]
public class GenericExtensionsTests
{
	[Fact]
	public void IsBetweenWhenValueIsInRange_WhenCalled_ShouldReturnTrue()
	{
		5.IsBetween(1, 10).Should().BeTrue();
	}

	[Fact]
	public void IsBetweenWhenValueIsOutOfRange_WhenCalled_ShouldReturnFalse()
	{
		11.IsBetween(1, 10).Should().BeFalse();
	}

	[Fact]
	public void IsDefaultWhenValueIsDefault_WhenCalled_ShouldReturnTrue()
	{
		default(int).IsDefault().Should().BeTrue();
	}

	[Fact]
	public void IsDefaultWhenValueIsNotDefault_WhenCalled_ShouldReturnFalse()
	{
		42.IsDefault().Should().BeFalse();
	}

	[Fact]
	public void IsNullWhenValueIsNotNull_WhenCalled_ShouldReturnFalse()
	{
		"test".IsNull().Should().BeFalse();
	}

	[Fact]
	public void IsNullWhenValueIsNull_WhenCalled_ShouldReturnTrue()
	{
		string? value = null;
		value.IsNull().Should().BeTrue();
	}

	[Fact]
	public void IsOneOfWhenValueIsInCandidates_WhenCalled_ShouldReturnTrue()
	{
		5.IsOneOf(1, 3, 5, 7).Should().BeTrue();
	}

	[Fact]
	public void IsOneOfWhenValueIsNotInCandidates_WhenCalled_ShouldReturnFalse()
	{
		6.IsOneOf(1, 3, 5, 7).Should().BeFalse();
	}

	[Fact]
	public void ThrowIfEmptyWhenEnumerableWithoutCountIsEmpty_WhenCalled_ShouldThrowArgumentException()
	{
		var enumerable = GetEmptyIterator();

		Action action = () => enumerable.ThrowIfEmpty();

		action.Should().Throw<ArgumentException>().WithMessage("Sequence cannot be empty.*");
	}

	[Fact]
	public void ThrowIfEmptyWhenReadOnlyCollectionIsEmpty_WhenCalled_ShouldThrowArgumentException()
	{
		IReadOnlyCollection<int> collection = new TestReadOnlyCollection<int>(Array.Empty<int>());

		Action action = () => collection.ThrowIfEmpty();

		action.Should().Throw<ArgumentException>().WithMessage("Sequence cannot be empty.*");
	}

	[Fact]
	public void ThrowIfEmptyWhenReadOnlyCollectionIsNotEmpty_WhenCalled_ShouldReturnCollection()
	{
		IReadOnlyCollection<int> collection = new TestReadOnlyCollection<int>(new[] { 42 });

		var result = collection.ThrowIfEmpty();

		result.Should().BeSameAs(collection);
	}

	[Fact]
	public void ThrowIfEmptyWhenSequenceIsEmpty_WhenCalled_ShouldThrowEmptyCollectionException()
	{
		var action = () => Array.Empty<int>().ThrowIfEmpty();
		action.Should().Throw<EmptyCollectionException>();
	}

	[Fact]
	public void ThrowIfEmptyWhenSequenceIsNotEmpty_WhenCalled_ShouldReturnSequence()
	{
		int[] array  = [1, 2, 3];
		int[] result = array.ThrowIfEmpty();
		result.Should().BeEquivalentTo(array);
	}

	[Fact]
	public void ThrowIfNullWhenValueIsNotNull_WhenCalled_ShouldReturnValue()
	{
		string result = "test".ThrowIfNull();
		result.Should().Be("test");
	}

	[Fact]
	public void ThrowIfNullWhenValueIsNull_WhenCalled_ShouldThrowArgumentNullException()
	{
		string? value  = null;
		var     action = () => value.ThrowIfNull();
		action.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public void ThrowIfWhenConditionIsFalse_WhenCalled_ShouldReturnValue()
	{
		int result = 5.ThrowIf(false, "param", "Custom message");
		result.Should().Be(5);
	}

	[Fact]
	public void ThrowIfWhenConditionIsTrue_WhenCalled_ShouldThrowArgumentException()
	{
		var action = () => 5.ThrowIf(true, "param", "Custom message");
		action.Should().Throw<ArgumentException>().WithMessage("Custom message*");
	}

	[Fact]
	public void ThrowIfWhenPredicateIsFalse_WhenCalled_ShouldReturnValue()
	{
		int result = 5.ThrowIf(x => x < 0);
		result.Should().Be(5);
	}

	[Fact]
	public void ThrowIfWhenPredicateIsTrue_WhenCalled_ShouldThrowArgumentException()
	{
		var action = () => 5.ThrowIf(x => x > 0);
		action.Should().Throw<ArgumentException>();
	}

	[Fact]
	public void Yield_WhenCalled_ShouldReturnEnumerableWithSingleItem()
	{
		var result = 42.Yield();
		result.Should().ContainSingle().Which.Should().Be(42);
	}

	private static IEnumerable<int> GetEmptyIterator()
	{
		yield break;
	}
}

internal sealed class TestReadOnlyCollection<T>(IEnumerable<T> items) : IReadOnlyCollection<T>
{
	private readonly T[] _items = items.ToArray();

	public int Count => _items.Length;

	public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)_items).GetEnumerator();

	IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();
}
