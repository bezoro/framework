using Bezoro.Core.Common.Extensions.Collections.Check;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Common.Extensions.Collections.Check;

[TestSubject(typeof(ArrayCheck))]
public static class ArrayCheckTests
{
	public class Unit
	{
		[Fact]
		public void AreEqual_WhenEqualParameters_ShouldReturnTrue()
		{
			int[] array1 = [1, 2, 3];
			int[] array2 = [1, 2, 3];

			int[,] array2d1 = new[,] { { 1, 2 }, { 3, 4 } };
			int[,] array2d2 = new[,] { { 1, 2 }, { 3, 4 } };

			array1.AreEqual(array2).Should().BeTrue();
			array2d1.AreEqual(array2d2).Should().BeTrue();
		}

		[Fact]
		public void AreEqual_WhenNotEqualParameters_ShouldReturnFalse()
		{
			int[] array1 = [1, 2, 3];
			int[] array2 = [1, 2, 4];
			int[] array3 = [1, 2];

			int[,] array2d1 = new[,] { { 1, 2 }, { 3, 4 } };
			int[,] array2d2 = new[,] { { 1, 2 }, { 3, 5 } };
			int[,] array2d3 = new[,] { { 1, 2 } };

			array1.AreEqual(array2).Should().BeFalse();
			array2d1.AreEqual(array2d2).Should().BeFalse();
			array1.AreEqual(array3).Should().BeFalse();
			array2d1.AreEqual(array2d3).Should().BeFalse();
		}

		[Fact]
		public void IsNullOrEmpty_WhenNotNullOrEmpty_ShouldReturnFalse()
		{
			int[,] notEmptyArray = new[,] { { 1, 2 }, { 3, 4 } };

			notEmptyArray.IsNullOrEmpty().Should().BeFalse();
		}

		[Fact]
		public void IsNullOrEmpty_WhenNullOrEmpty_ShouldReturnTrue()
		{
			int[,]? nullArray  = null;
			var     emptyArray = new int[0, 0];

			nullArray.IsNullOrEmpty().Should().BeTrue();
			emptyArray.IsNullOrEmpty().Should().BeTrue();
		}
	}
}
