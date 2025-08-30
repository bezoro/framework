using Bezoro.Core.Common.Extensions.Collections.Search;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Common.Extensions.Collections.Search;

[TestSubject(typeof(ArraySearchExtensions))]
public static class ArraySearchExtensionsTests
{
	public class Unit
	{
		[Fact]
		public void Contains_WhenElementDoesNotExist_ReturnsFalse()
		{
			string[] array = ["first", "second", "third"];
			array.Contains("fourth", out int index).Should().BeFalse();
			index.Should().Be(-1);
		}

		[Fact]
		public void Contains_WhenElementExists_ReturnsTrue()
		{
			string[] array = ["first", "second", "third"];
			array.Contains("second", out int index).Should().BeTrue();
			index.Should().Be(1);
		}

		[Fact]
		public void ContainsStruct_WhenElementDoesNotExist_ReturnsFalse()
		{
			int[] array = [1, 2, 3];
			array.ContainsStruct(4, out int index).Should().BeFalse();
			index.Should().Be(-1);
		}

		[Fact]
		public void ContainsStruct_WhenElementExists_ReturnsTrue()
		{
			int[] array = [1, 2, 3];
			array.ContainsStruct(2, out int index).Should().BeTrue();
			index.Should().Be(1);
		}

		[Fact]
		public void CountEmptyIndices_ReturnsCorrectCount()
		{
			string?[] array = ["first", null, "third", null];
			array.CountEmptyIndices().Should().Be(2);
		}

		[Fact]
		public void CountEmptyIndices_WithValidSize_ReturnsCorrectCount()
		{
			var array = new string[5];
			array[0] = "first";
			array[1] = "second";
			array.CountEmptyIndices(2).Should().Be(3);
		}

		[Fact]
		public void CountFilledIndices_ReturnsCorrectCount()
		{
			string?[] array = ["first", null, "third", null];
			array.CountFilledIndices().Should().Be(2);
		}

		[Fact]
		public void TryFindFirstEmptyIndex_WhenEmptyElementExists_ReturnsTrue()
		{
			string?[] array = ["first", null, "third"];
			array.TryFindFirstEmptyIndex(out int index).Should().BeTrue();
			index.Should().Be(1);
		}

		[Fact]
		public void TryFindFirstEmptyIndex_WhenNoEmptyElement_ReturnsFalse()
		{
			string[] array = ["first", "second", "third"];
			array.TryFindFirstEmptyIndex(out int index).Should().BeFalse();
			index.Should().Be(-1);
		}
	}
}
