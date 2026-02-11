using System;
using Bezoro.Core.Helpers;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Helpers;

[TestSubject(typeof(ArrayConverter))]
public class ArrayConverterTests
{
	#region Flatten

	[Fact]
	public void Flatten_ShouldFlattenInRowMajorOrder()
	{
		int[,] input = new[,]
		{
			{ 1, 2, 3 },
			{ 4, 5, 6 }
		};

		int[] result = ArrayConverter.Flatten(input);

		result.Should().Equal(1, 2, 3, 4, 5, 6);
		result.Length.Should().Be(6);
	}

	[Fact]
	public void Flatten_ShouldReturnEmptyArray_WhenZeroColumns()
	{
		var input = new int[2, 0];

		int[] result = ArrayConverter.Flatten(input);

		result.Should().BeEmpty();
	}

	[Fact]
	public void Flatten_ShouldReturnEmptyArray_WhenZeroRows()
	{
		var input = new int[0, 3];

		int[] result = ArrayConverter.Flatten(input);

		result.Should().BeEmpty();
	}

	[Fact]
	public void Flatten_ShouldThrow_WhenInputIsNull()
	{
		var act = () => ArrayConverter.Flatten<int>(null!);
		act.Should().Throw<ArgumentNullException>().WithParameterName("from");
	}

	[Fact]
	public void Flatten_ShouldWorkWithReferenceTypes()
	{
		var input = new string[1, 1];
		input[0, 0] = "alpha";

		string[] result = ArrayConverter.Flatten(input);

		result.Should().Equal("alpha");
	}

	#endregion

	#region Reshape

	[Fact]
	public void Reshape_ShouldArrangeElementsInRowMajorOrder()
	{
		int[] input = { 1, 2, 3, 4, 5, 6 };

		int[,] result = ArrayConverter.Reshape(input, 2, 3);

		result.GetLength(0).Should().Be(2);
		result.GetLength(1).Should().Be(3);
		result[0, 0].Should().Be(1);
		result[0, 2].Should().Be(3);
		result[1, 0].Should().Be(4);
		result[1, 2].Should().Be(6);
	}

	[Fact]
	public void Reshape_ShouldReturnEmptyArray_WhenInputIsEmpty()
	{
		int[] input = Array.Empty<int>();

		int[,] result = ArrayConverter.Reshape(input, 0, 0);

		result.Length.Should().Be(0);
	}

	[Fact]
	public void Reshape_ShouldThrow_WhenInputIsNull()
	{
		var act = () => ArrayConverter.Reshape<int>(null!, 1, 1);
		act.Should().Throw<ArgumentNullException>().WithParameterName("from");
	}

	[Fact]
	public void Reshape_ShouldThrow_WhenRowsIsNegative()
	{
		var act = () => ArrayConverter.Reshape(new[] { 1 }, -1, 1);
		act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("rows");
	}

	[Fact]
	public void Reshape_ShouldThrow_WhenColumnsIsNegative()
	{
		var act = () => ArrayConverter.Reshape(new[] { 1 }, 1, -1);
		act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("columns");
	}

	[Fact]
	public void Reshape_ShouldThrow_WhenLengthDoesNotMatchShape()
	{
		var act = () => ArrayConverter.Reshape(new[] { 1, 2, 3 }, 2, 2);
		act.Should().Throw<ArgumentException>().WithParameterName("from");
	}

	[Fact]
	public void Reshape_ShouldWorkWithReferenceTypes()
	{
		string[] input = { "a", "b", "c", "d" };

		string[,] result = ArrayConverter.Reshape(input, 2, 2);

		result[0, 0].Should().Be("a");
		result[0, 1].Should().Be("b");
		result[1, 0].Should().Be("c");
		result[1, 1].Should().Be("d");
	}

	[Fact]
	public void Flatten_And_Reshape_ShouldRoundTrip()
	{
		int[,] original = new[,]
		{
			{ 1, 2, 3 },
			{ 4, 5, 6 }
		};

		int[] flat = ArrayConverter.Flatten(original);
		int[,] restored = ArrayConverter.Reshape(flat, 2, 3);

		restored.GetLength(0).Should().Be(original.GetLength(0));
		restored.GetLength(1).Should().Be(original.GetLength(1));

		for (int i = 0; i < 2; i++)
		for (int j = 0; j < 3; j++)
			restored[i, j].Should().Be(original[i, j]);
	}

	#endregion
}
