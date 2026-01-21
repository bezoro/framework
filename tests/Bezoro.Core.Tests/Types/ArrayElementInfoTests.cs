using System;
using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Common.Primitives;

[TestSubject(typeof(ArrayElementInfo<>))]
public static class ArrayElementInfoTests
{
	public static class UnitTests
	{
		public class ConstructorTests
		{
			[Fact]
			public void WhenArrayLengthZeroAndIndexNull_ShouldSucceed()
			{
				var info = new ArrayElementInfo<int>(null, 0, 0);

				info.Index.Should().BeNull();
				info.Element.Should().Be(0);
				info.ArrayLength.Should().Be(0);
				info.IsFound.Should().BeFalse();
			}

			[Fact]
			public void WhenArrayLengthZeroAndIndexZero_ShouldThrow()
			{
				var act = () => new ArrayElementInfo<int>(0, 42, 0);

				act.Should().Throw<ArgumentOutOfRangeException>()
				   .WithParameterName("index");
			}

			[Fact]
			public void WhenFoundElementIsNull_ShouldPreserveNull()
			{
				var info = new ArrayElementInfo<string>(2, null, 5);

				info.Index.Should().Be(2);
				info.Element.Should().BeNull();
				info.IsFound.Should().BeTrue();
			}

			[Fact]
			public void WhenIndexEqualsArrayLength_ShouldThrow()
			{
				var act = () => new ArrayElementInfo<int>(5, 42, 5);

				act.Should().Throw<ArgumentOutOfRangeException>()
				   .WithParameterName("index");
			}

			[Fact]
			public void WhenIndexGreaterThanArrayLength_ShouldThrow()
			{
				var act = () => new ArrayElementInfo<string>(10, "x", 3);

				act.Should().Throw<ArgumentOutOfRangeException>()
				   .WithParameterName("index");
			}

			[Fact]
			public void WhenIndexIsMaxValueMinusOne_ShouldSucceed()
			{
				var info = new ArrayElementInfo<int>(uint.MaxValue - 1, 42, uint.MaxValue);

				info.Index.Should().Be(uint.MaxValue - 1);
				info.ArrayLength.Should().Be(uint.MaxValue);
				info.IsFound.Should().BeTrue();
			}

			[Fact]
			public void WhenReferenceType_ShouldSetProperties()
			{
				var info = new ArrayElementInfo<string>(2, "x", 3);

				info.Index.Should().Be(2);
				info.Element.Should().Be("x");
				info.ArrayLength.Should().Be(3);
				info.IsFound.Should().BeTrue();
				ArrayElementInfo<string>.ElementType.Should().Be<string>();
				info.RuntimeElementType.Should().Be<string>();
			}

			[Fact]
			public void WhenValueType_ShouldSetProperties()
			{
				var info = new ArrayElementInfo<int>(0, 42, 10);

				info.Index.Should().Be(0);
				info.Element.Should().Be(42);
				info.ArrayLength.Should().Be(10);
				info.IsFound.Should().BeTrue();
				ArrayElementInfo<int>.ElementType.Should().Be<int>();
				info.RuntimeElementType.Should().Be<int>();
			}
		}

		public class DeconstructorTests
		{
			[Fact]
			public void WhenCalled_ShouldReturnFields()
			{
				var info = new ArrayElementInfo<int>(3, 7, 11);

				(uint? idx, int elem, uint len) = info;

				idx.Should().Be(3);
				elem.Should().Be(7);
				len.Should().Be(11);
			}
		}

		public class IsFoundTests
		{
			[Theory]
			[InlineData(null, false)]
			[InlineData(0u,   true)]
			[InlineData(5u,   true)]
			public void WhenIsFound_ShouldReflectIndex(uint? index, bool expected)
			{
				var info = new ArrayElementInfo<object>(index, new(), index.HasValue ? index.Value + 1 : 0);

				info.IsFound.Should().Be(expected);
			}
		}

		public class NotFoundTests
		{
			[Fact]
			public void WhenNotFound_ShouldCreateNotFoundInstance()
			{
				var info = ArrayElementInfo<string>.NotFound("y", 9);

				info.Index.Should().Be(null);
				info.IsFound.Should().BeFalse();
				info.Element.Should().Be("y");
				info.ArrayLength.Should().Be(9);
			}
		}

		public class RuntimeElementTypeTests
		{
			[Fact]
			public void WhenElementIsNull_ShouldFallbackToArrayType()
			{
				var info = new ArrayElementInfo<string>(5, null, 7);

				info.RuntimeElementType.Should().Be<string>();
			}

			[Fact]
			public void WhenNullableHasValue_ShouldBeUnderlyingType()
			{
				var info = new ArrayElementInfo<int?>(1, 5, 3);

				info.RuntimeElementType.Should().Be<int>(); // boxing Nullable<T> with value yields underlying T
				ArrayElementInfo<int?>.ElementType.Should().Be<int?>();
			}

			[Fact]
			public void WhenNullableIsNull_ShouldBeArrayType()
			{
				var info = new ArrayElementInfo<int?>(null, null, 3);

				info.RuntimeElementType.Should().Be<int?>();
			}
		}

		public class ToStringTests
		{
			[Fact]
			public void WhenNullElement_ShouldFormatAsNullString()
			{
				var info = new ArrayElementInfo<string>(null, null, 5);

				info.ToString().Should().Be("ArrayElementInfo<String> { NotFound, Element = <null>, ArrayLength = 5 }");
			}

			[Fact]
			public void WhenValidElements_ShouldFormatAsExpected()
			{
				var info = new ArrayElementInfo<string>(1, "foo", 5);

				info.ToString().Should().Be("ArrayElementInfo<String> { Index = 1, Element = foo, ArrayLength = 5 }");
			}
		}
	}
}
