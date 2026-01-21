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

		public class FoundTests
		{
			[Fact]
			public void WhenFound_ShouldCreateFoundInstance()
			{
				var info = ArrayElementInfo<string>.Found(2, "hello", 10);

				info.Index.Should().Be(2);
				info.IsFound.Should().BeTrue();
				info.Element.Should().Be("hello");
				info.ArrayLength.Should().Be(10);
			}

			[Fact]
			public void WhenIndexOutOfRange_ShouldThrow()
			{
				var act = () => ArrayElementInfo<string>.Found(10, "test", 5);

				act.Should().Throw<ArgumentOutOfRangeException>()
				   .WithParameterName("index");
			}
		}

		public class ImplicitBoolConversionTests
		{
			[Fact]
			public void WhenFound_ShouldReturnTrue()
			{
				var info = ArrayElementInfo<int>.Found(0, 42, 5);

				bool result = info;

				result.Should().BeTrue();
			}

			[Fact]
			public void WhenNotFound_ShouldReturnFalse()
			{
				var info = ArrayElementInfo<int>.NotFound(42, 5);

				bool result = info;

				result.Should().BeFalse();
			}

			[Fact]
			public void WhenUsedInIfStatement_ShouldWork()
			{
				var found = ArrayElementInfo<string>.Found(0, "test", 1);
				var notFound = ArrayElementInfo<string>.NotFound("missing", 5);

				var foundResult = false;
				var notFoundResult = true;

				if (found)
					foundResult = true;

				if (notFound)
					notFoundResult = false;

				foundResult.Should().BeTrue();
				notFoundResult.Should().BeTrue();
			}
		}

		public class ExplicitUintConversionTests
		{
			[Fact]
			public void WhenFound_ShouldReturnIndex()
			{
				var info = ArrayElementInfo<string>.Found(3, "hello", 10);

				uint? index = (uint?)info;

				index.Should().Be(3);
			}

			[Fact]
			public void WhenNotFound_ShouldReturnNull()
			{
				var info = ArrayElementInfo<string>.NotFound("missing", 10);

				uint? index = (uint?)info;

				index.Should().BeNull();
			}
		}

		public class GetElementOrDefaultTests
		{
			[Fact]
			public void WhenFound_ShouldReturnElement()
			{
				var info = ArrayElementInfo<string>.Found(0, "hello", 5);

				info.GetElementOrDefault().Should().Be("hello");
			}

			[Fact]
			public void WhenNotFound_ShouldReturnSearchedElement()
			{
				var info = ArrayElementInfo<int>.NotFound(42, 5);

				info.GetElementOrDefault().Should().Be(42);
			}

			[Fact]
			public void WithDefaultValue_WhenFound_ShouldReturnElement()
			{
				var info = ArrayElementInfo<string>.Found(0, "hello", 5);

				info.GetElementOrDefault("fallback").Should().Be("hello");
			}

			[Fact]
			public void WithDefaultValue_WhenNotFound_ShouldReturnDefaultValue()
			{
				var info = ArrayElementInfo<string>.NotFound(null, 5);

				info.GetElementOrDefault("fallback").Should().Be("fallback");
			}

			[Fact]
			public void WithDefaultValue_WhenFoundButElementIsNull_ShouldReturnDefaultValue()
			{
				var info = ArrayElementInfo<string>.Found(0, null!, 5);

				info.GetElementOrDefault("fallback").Should().Be("fallback");
			}
		}

		public class TryGetElementTests
		{
			[Fact]
			public void WhenFound_ShouldReturnTrueAndSetOutParams()
			{
				var info = ArrayElementInfo<string>.Found(2, "hello", 10);

				var result = info.TryGetElement(out var element, out var index);

				result.Should().BeTrue();
				element.Should().Be("hello");
				index.Should().Be(2u);
			}

			[Fact]
			public void WhenNotFound_ShouldReturnFalseAndSetDefaults()
			{
				var info = ArrayElementInfo<string>.NotFound("missing", 10);

				var result = info.TryGetElement(out var element, out var index);

				result.Should().BeFalse();
				element.Should().BeNull();
				index.Should().Be(0u);
			}

			[Fact]
			public void WhenFoundWithValueType_ShouldReturnTrueAndSetOutParams()
			{
				var info = ArrayElementInfo<int>.Found(5, 42, 10);

				var result = info.TryGetElement(out var element, out var index);

				result.Should().BeTrue();
				element.Should().Be(42);
				index.Should().Be(5u);
			}
		}

#if NET6_0_OR_GREATER
		public class TryFormatTests
		{
			[Fact]
			public void WhenFound_ShouldFormatCorrectly()
			{
				var info = ArrayElementInfo<string>.Found(1, "foo", 5);
				Span<char> buffer = stackalloc char[256];

				var result = info.TryFormat(buffer, out var charsWritten, default, null);

				result.Should().BeTrue();
				buffer[..charsWritten].ToString().Should().Be("ArrayElementInfo<String> { Index = 1, Element = foo, ArrayLength = 5 }");
			}

			[Fact]
			public void WhenNotFound_ShouldFormatCorrectly()
			{
				var info = ArrayElementInfo<string>.NotFound(null, 5);
				Span<char> buffer = stackalloc char[256];

				var result = info.TryFormat(buffer, out var charsWritten, default, null);

				result.Should().BeTrue();
				buffer[..charsWritten].ToString().Should().Be("ArrayElementInfo<String> { NotFound, Element = <null>, ArrayLength = 5 }");
			}

			[Fact]
			public void WhenBufferTooSmall_ShouldReturnFalse()
			{
				var info = ArrayElementInfo<string>.Found(1, "foo", 5);
				Span<char> buffer = stackalloc char[10];

				var result = info.TryFormat(buffer, out var charsWritten, default, null);

				result.Should().BeFalse();
			}

			[Fact]
			public void ToStringWithFormat_ShouldReturnSameAsToString()
			{
				var info = ArrayElementInfo<string>.Found(1, "foo", 5);

				var result = info.ToString(null, null);

				result.Should().Be(info.ToString());
			}
		}
#endif
	}
}
