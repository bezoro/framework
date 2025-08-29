using System;
using Bezoro.Core.Common.Extensions;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Common.Extensions;

[TestSubject(typeof(GenericExtensions))]
public abstract class GenericExtensionsTests
{
	public class Unit
	{
		[Fact]
		public void IsBetween_WhenValueIsInRange_ReturnsTrue()
		{
			5.IsBetween(1, 10).Should().BeTrue();
		}

		[Fact]
		public void IsBetween_WhenValueIsOutOfRange_ReturnsFalse()
		{
			11.IsBetween(1, 10).Should().BeFalse();
		}

		[Fact]
		public void IsDefault_WhenValueIsDefault_ReturnsTrue()
		{
			default(int).IsDefault().Should().BeTrue();
		}

		[Fact]
		public void IsDefault_WhenValueIsNotDefault_ReturnsFalse()
		{
			42.IsDefault().Should().BeFalse();
		}

		[Fact]
		public void IsNull_WhenValueIsNotNull_ReturnsFalse()
		{
			"test".IsNull().Should().BeFalse();
		}

		[Fact]
		public void IsNull_WhenValueIsNull_ReturnsTrue()
		{
			string? value = null;
			value.IsNull().Should().BeTrue();
		}

		[Fact]
		public void IsOneOf_WhenValueIsInCandidates_ReturnsTrue()
		{
			5.IsOneOf(1, 3, 5, 7).Should().BeTrue();
		}

		[Fact]
		public void IsOneOf_WhenValueIsNotInCandidates_ReturnsFalse()
		{
			6.IsOneOf(1, 3, 5, 7).Should().BeFalse();
		}

		[Fact]
		public void ThrowIf_WhenConditionIsFalse_ReturnsValue()
		{
			int result = 5.ThrowIf(false, "param", "Custom message");
			result.Should().Be(5);
		}

		[Fact]
		public void ThrowIf_WhenConditionIsTrue_ThrowsArgumentException()
		{
			var action = () => 5.ThrowIf(true, "param", "Custom message");
			action.Should().Throw<ArgumentException>().WithMessage("Custom message*");
		}

		[Fact]
		public void ThrowIf_WhenPredicateIsFalse_ReturnsValue()
		{
			int result = 5.ThrowIf(x => x < 0);
			result.Should().Be(5);
		}

		[Fact]
		public void ThrowIf_WhenPredicateIsTrue_ThrowsArgumentException()
		{
			var action = () => 5.ThrowIf(x => x > 0);
			action.Should().Throw<ArgumentException>();
		}

		[Fact]
		public void ThrowIfEmpty_WhenSequenceIsEmpty_ThrowsArgumentException()
		{
			var action = () => Array.Empty<int>().ThrowIfEmpty();
			action.Should().Throw<ArgumentException>();
		}

		[Fact]
		public void ThrowIfEmpty_WhenSequenceIsNotEmpty_ReturnsSequence()
		{
			int[] array  = [1, 2, 3];
			int[] result = array.ThrowIfEmpty();
			result.Should().BeEquivalentTo(array);
		}

		[Fact]
		public void ThrowIfNull_WhenValueIsNotNull_ReturnsValue()
		{
			string result = "test".ThrowIfNull();
			result.Should().Be("test");
		}

		[Fact]
		public void ThrowIfNull_WhenValueIsNull_ThrowsArgumentNullException()
		{
			string? value  = null;
			var     action = () => value.ThrowIfNull();
			action.Should().Throw<ArgumentNullException>();
		}

		[Fact]
		public void Yield_ReturnsEnumerableWithSingleItem()
		{
			var result = 42.Yield();
			result.Should().ContainSingle().Which.Should().Be(42);
		}
	}
}
