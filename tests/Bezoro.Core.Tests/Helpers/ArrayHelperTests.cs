using System;
using Bezoro.Core.Helpers;
using Bezoro.Core.Types.Exceptions;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Common.Helpers;

[TestSubject(typeof(ArrayHelper))]
public static class ArrayHelperTests
{
	public class Unit
	{
		[Fact]
		public void Add_ShouldPlaceItemInFirstNullSlot()
		{
			var arr = new string?[] { null, null };

			string?[] result = ArrayHelper.Add(ref arr, "alpha");

			result.Should().BeSameAs(arr);
			arr[0].Should().Be("alpha");
			arr[1].Should().BeNull();

			ArrayHelper.Add(ref arr, "beta");
			arr[0].Should().Be("alpha");
			arr[1].Should().Be("beta");
		}

		[Fact]
		public void Add_ShouldThrow_WhenArrayIsEmpty()
		{
			string?[] arr = [];

			Action act = () => ArrayHelper.Add(ref arr, "x");

			act.Should().Throw<EmptyCollectionException>();
		}

		[Fact]
		public void Add_ShouldThrow_WhenArrayIsFull()
		{
			string[] arr = new[] { "a" };

			Action act = () => ArrayHelper.Add(ref arr, "b");

			act.Should().Throw<ArrayIsFullException>();
		}

		[Fact]
		public void Add_ShouldThrow_WhenArrayIsNull()
		{
			string?[] arr = null!;

			Action act = () => ArrayHelper.Add(ref arr, "x");

			act.Should().Throw<ArgumentNullException>();
		}

		[Fact]
		public void AddUnique_ShouldAdd_WhenNotPresent()
		{
			string?[] arr = new[] { null, "b", null };

			string?[] result = ArrayHelper.AddUnique(ref arr, "a");

			result.Should().BeSameAs(arr);
			arr.Should().Contain("a");
		}

		[Fact]
		public void AddUnique_ShouldNotAdd_WhenAlreadyPresent()
		{
			string?[] arr = new[] { "a", null };

			string?[] result = ArrayHelper.AddUnique(ref arr, "a");

			result.Should().BeSameAs(arr);
			arr[0].Should().Be("a");
			arr[1].Should().BeNull();
		}

		[Fact]
		public void AddUnique_ShouldThrow_WhenArrayIsEmpty()
		{
			string?[] arr = [];

			Action act = () => ArrayHelper.AddUnique(ref arr, "x");

			act.Should().Throw<EmptyCollectionException>();
		}

		[Fact]
		public void AddUnique_ShouldThrow_WhenArrayIsNull()
		{
			string?[] arr = null!;

			Action act = () => ArrayHelper.AddUnique(ref arr, "x");

			act.Should().Throw<ArgumentNullException>();
		}

		[Fact]
		public void AddUnique_ShouldThrow_WhenNoSpaceAvailable_AndNotPresent()
		{
			string[] arr = new[] { "a", "b" };

			Action act = () => ArrayHelper.AddUnique(ref arr, "c");

			act.Should().Throw<ArrayIsFullException>();
		}

		[Fact]
		public void CompareArrays_ShouldReturnFalse_ForDifferentLengths()
		{
			int[] a = [1, 2];
			int[] b = [1, 2, 3];

			ArrayHelper.CompareArrays(a, b).Should().BeFalse();
		}

		[Fact]
		public void CompareArrays_ShouldReturnFalse_WhenAnyElementDiffers()
		{
			int[] a = [1, 2, 3];
			int[] b = [1, 99, 3];

			ArrayHelper.CompareArrays(a, b).Should().BeFalse();
		}

		[Fact]
		public void CompareArrays_ShouldReturnTrue_ForEqualArrays()
		{
			int[] a = [1, 2, 3];
			int[] b = [1, 2, 3];

			ArrayHelper.CompareArrays(a, b).Should().BeTrue();
		}

		[Fact]
		public void CompareArrays_ShouldThrow_WhenEitherArrayIsEmpty()
		{
			int[] a = [];
			int[] b = [1, 2];

			Action act = () => ArrayHelper.CompareArrays(a, b);

			act.Should().Throw<EmptyCollectionException>();
		}

		[Fact]
		public void CompareArrays_ShouldThrow_WhenFirstIsNull()
		{
			int[] a = null!;
			int[] b = [1, 2];

			Action act = () => ArrayHelper.CompareArrays(a, b);

			act.Should().Throw<ArgumentNullException>();
		}

		[Fact]
		public void CompareArrays_ShouldThrow_WhenSecondIsNull()
		{
			int[] a = [1, 2];
			int[] b = null!;

			Action act = () => ArrayHelper.CompareArrays(a, b);

			act.Should().Throw<ArgumentNullException>();
		}

		[Fact]
		public void RemoveElement_ShouldNoop_WhenItemNotFound()
		{
			string[] arr = new[] { "x", "y" };

			ArrayHelper.RemoveElement(ref arr!, "z");

			arr.Should().Equal("x", "y");
		}

		[Fact]
		public void RemoveElement_ShouldSetFoundElementToNull_AndReturnSameArray()
		{
			string[] arr = new[] { "a", "b", "c" };

			string?[] result = ArrayHelper.RemoveElement(ref arr!, "b");

			result.Should().BeSameAs(arr);
			arr[0].Should().Be("a");
			arr[1].Should().BeNull();
			arr[2].Should().Be("c");
		}

		[Fact]
		public void RemoveElement_ShouldThrow_WhenArrayIsEmpty()
		{
			string?[] arr = [];

			Action act = () => ArrayHelper.RemoveElement(ref arr, "x");

			act.Should().Throw<EmptyCollectionException>();
		}

		[Fact]
		public void RemoveElement_ShouldThrow_WhenArrayIsNull()
		{
			string?[] arr = null!;

			Action act = () => ArrayHelper.RemoveElement(ref arr, "x");

			act.Should().Throw<ArgumentNullException>();
		}
	}
}
