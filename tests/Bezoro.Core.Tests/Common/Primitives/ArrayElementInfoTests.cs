using Bezoro.Core.Common.Primitives;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Common.Primitives;

[TestSubject(typeof(ArrayElementInfo<int>))]
public static class ArrayElementInfoTests
{
	public class Unit
	{
		[Fact]
		public void Constructor_ShouldSetProperties_ForReferenceType()
		{
			var info = new ArrayElementInfo<string>(2, "x", 3);

			info.Index.Should().Be(2);
			info.Element.Should().Be("x");
			info.ArrayLength.Should().Be(3);
			info.IsFound.Should().BeTrue();
			ArrayElementInfo<string>.ArrayType.Should().Be(typeof(string));
			info.ElementType.Should().Be(typeof(string));
		}

		[Fact]
		public void Constructor_ShouldSetProperties_ForValueType()
		{
			var info = new ArrayElementInfo<int>(0, 42, 10);

			info.Index.Should().Be(0);
			info.Element.Should().Be(42);
			info.ArrayLength.Should().Be(10);
			info.IsFound.Should().BeTrue();
			ArrayElementInfo<int>.ArrayType.Should().Be(typeof(int));
			info.ElementType.Should().Be(typeof(int));
		}

		[Fact]
		public void ElementType_ShouldFallbackToArrayType_WhenElementIsNull_ForReferenceType()
		{
			var info = new ArrayElementInfo<string>(5, null, 7);

			info.ElementType.Should().Be(typeof(string));
		}

		[Fact]
		public void ElementType_ShouldBeUnderlyingType_WhenNullableHasValue()
		{
			var info = new ArrayElementInfo<int?>(1, 5, 3);

			info.ElementType.Should().Be(typeof(int)); // boxing Nullable<T> with value yields underlying T
			ArrayElementInfo<int?>.ArrayType.Should().Be(typeof(int?));
		}

		[Fact]
		public void ElementType_ShouldBeArrayType_WhenNullableIsNull()
		{
			var info = new ArrayElementInfo<int?>(-1, null, 3);

			info.ElementType.Should().Be(typeof(int?));
		}

		[Fact]
		public void NotFound_ShouldCreateNotFoundInstance()
		{
			var info = ArrayElementInfo<string>.NotFound("y", 9);

			info.Index.Should().Be(-1);
			info.IsFound.Should().BeFalse();
			info.Element.Should().Be("y");
			info.ArrayLength.Should().Be(9);
		}

		[Fact]
		public void ToString_ShouldFormatAsExpected()
		{
			var info = new ArrayElementInfo<string>(1, "foo", 5);

			info.ToString().Should().Be("[Index: 1, Element: foo, Length: 5, Type: String]");
		}

		[Fact]
		public void ToString_ShouldFormatNullElementAsEmptyString()
		{
			var info = new ArrayElementInfo<string>(-1, null, 5);

			info.ToString().Should().Be("[Index: -1, Element: , Length: 5, Type: String]");
		}

		[Fact]
		public void Deconstruct_ShouldReturnFields()
		{
			var info = new ArrayElementInfo<int>(3, 7, 11);

			(int idx, int elem, int len) = info;

			idx.Should().Be(3);
			elem.Should().Be(7);
			len.Should().Be(11);
		}

		[Theory]
		[InlineData(-1, false)]
		[InlineData(0,  true)]
		[InlineData(5,  true)]
		public void IsFound_ShouldReflectIndex(int index, bool expected)
		{
			var info = new ArrayElementInfo<object>(index, new(), 1);

			info.IsFound.Should().Be(expected);
		}
	}
}
