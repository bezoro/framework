using System;
using Bezoro.Core.Helpers;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Common.Helpers;

[TestSubject(typeof(ArrayConverter))]
public static class ArrayConverterTests
{
	public class Unit
	{
		[Fact]
		public void From2Dto1D_ShouldFlattenInRowMajorOrder()
		{
			int[,] input = new[,]
			{
				{ 1, 2, 3 },
				{ 4, 5, 6 }
			};

			int[] result = ArrayConverter.From2Dto1D(input);

			result.Should().Equal(1, 2, 3, 4, 5, 6);
			result.Length.Should().Be(6);
		}

		[Fact]
		public void From2Dto1D_ShouldReturnEmptyArray_WhenZeroColumns()
		{
			var input = new int[2, 0];

			int[] result = ArrayConverter.From2Dto1D(input);

			result.Should().BeEmpty();
		}

		[Fact]
		public void From2Dto1D_ShouldReturnEmptyArray_WhenZeroRows()
		{
			var input = new int[0, 3];

			int[] result = ArrayConverter.From2Dto1D(input);

			result.Should().BeEmpty();
		}

		[Fact]
		public void From2Dto1D_ShouldThrow_WhenInputIsNull()
		{
			var act = () => ArrayConverter.From2Dto1D<int>(null!);
			act.Should().Throw<ArgumentNullException>().WithParameterName("from");
		}

		[Fact]
		public void From2Dto1D_ShouldWorkWithReferenceTypes()
		{
			var input = new string[1, 1];
			input[0, 0] = "alpha";

			string[] result = ArrayConverter.From2Dto1D(input);

			result.Should().Equal("alpha");
		}
	}
}
