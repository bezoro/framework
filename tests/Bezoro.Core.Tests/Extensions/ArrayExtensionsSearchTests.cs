using Bezoro.Core.Extensions;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Extensions;

[TestSubject(typeof(ArrayExtensions))]
public class ArrayExtensionsProcessTests
{
	[Fact]
	public void ContainsWhenElementDoesNotExist_WhenCalled_ShouldReturnFalse()
	{
		string[] array = ["first", "second", "third"];
		array.Contains("fourth", out int index).Should().BeFalse();
		index.Should().Be(-1);
	}

	[Fact]
	public void ContainsWhenElementExists_WhenCalled_ShouldReturnTrue()
	{
		string[] array = ["first", "second", "third"];
		array.Contains("second", out int index).Should().BeTrue();
		index.Should().Be(1);
	}

	[Fact]
	public void ContainsStructWhenElementDoesNotExist_WhenCalled_ShouldReturnFalse()
	{
		int[] array = [1, 2, 3];
		array.ContainsStruct(4, out int index).Should().BeFalse();
		index.Should().Be(-1);
	}

	[Fact]
	public void ContainsStructWhenElementExists_WhenCalled_ShouldReturnTrue()
	{
		int[] array = [1, 2, 3];
		array.ContainsStruct(2, out int index).Should().BeTrue();
		index.Should().Be(1);
	}

	[Fact]
	public void CountEmptyIndices_WhenCalled_ShouldReturnCorrectCount()
	{
		string?[] array = ["first", null, "third", null];
		array.CountEmptyIndices().Should().Be(2);
	}

	[Fact]
	public void CountEmptyIndicesWithValidSize_WhenCalled_ShouldReturnCorrectCount()
	{
		var array = new string[5];
		array[0] = "first";
		array[1] = "second";
		array.CountEmptyIndices(2).Should().Be(3);
	}

	[Fact]
	public void CountFilledIndices_WhenCalled_ShouldReturnCorrectCount()
	{
		string?[] array = ["first", null, "third", null];
		array.CountFilledIndices().Should().Be(2);
	}

	[Fact]
	public void TryFindFirstEmptyIndexWhenEmptyElementExists_WhenCalled_ShouldReturnTrue()
	{
		string?[] array = ["first", null, "third"];
		array.TryFindFirstEmptyIndex(out int index).Should().BeTrue();
		index.Should().Be(1);
	}

	[Fact]
	public void TryFindFirstEmptyIndexWhenNoEmptyElement_WhenCalled_ShouldReturnFalse()
	{
		string[] array = ["first", "second", "third"];
		array.TryFindFirstEmptyIndex(out int index).Should().BeFalse();
		index.Should().Be(-1);
	}
}
