using System;
using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Common.Primitives;

[TestSubject(typeof(Percent))]
public static class PercentTests
{
	public static class UnitTests
	{
		public class Constructors
		{
			[Fact]
			public void WhenValidValue_ShouldCreateObject()
			{
				var p = new Percent(10);

				p.Value.Should().Be(10);
			}

			[Fact]
			public void WhenValueOver100_ShouldThrow()
			{
				var act = () => new Percent(101);

				act.Should().Throw<ArgumentOutOfRangeException>();
			}

			[Fact]
			public void WhenBoundaryValue0_ShouldCreateObject()
			{
				var p = new Percent(0);

				p.Value.Should().Be(0);
			}

			[Fact]
			public void WhenBoundaryValue100_ShouldCreateObject()
			{
				var p = new Percent(100);

				p.Value.Should().Be(100);
			}
		}

		public class StaticFields
		{
			[Fact]
			public void Zero_ShouldBe0()
			{
				Percent.Zero.Value.Should().Be(0);
			}

			[Fact]
			public void Quarter_ShouldBe25()
			{
				Percent.Quarter.Value.Should().Be(25);
			}

			[Fact]
			public void Half_ShouldBe50()
			{
				Percent.Half.Value.Should().Be(50);
			}

			[Fact]
			public void ThreeQuarters_ShouldBe75()
			{
				Percent.ThreeQuarters.Value.Should().Be(75);
			}

			[Fact]
			public void Ninety_ShouldBe90()
			{
				Percent.Ninety.Value.Should().Be(90);
			}

			[Fact]
			public void Full_ShouldBe100()
			{
				Percent.Full.Value.Should().Be(100);
			}
		}

		public class EqualityOperator
		{
			[Fact]
			public void WhenEqual_ShouldReturnTrue()
			{
				var a = new Percent(50);
				var b = new Percent(50);

				(a == b).Should().BeTrue();
			}

			[Fact]
			public void WhenNotEqual_ShouldReturnFalse()
			{
				var a = new Percent(50);
				var b = new Percent(75);

				(a == b).Should().BeFalse();
			}
		}

		public class InequalityOperator
		{
			[Fact]
			public void WhenNotEqual_ShouldReturnTrue()
			{
				var a = new Percent(50);
				var b = new Percent(75);

				(a != b).Should().BeTrue();
			}

			[Fact]
			public void WhenEqual_ShouldReturnFalse()
			{
				var a = new Percent(50);
				var b = new Percent(50);

				(a != b).Should().BeFalse();
			}
		}

		public class ComparisonOperators
		{
			[Fact]
			public void LessThan_WhenLess_ShouldReturnTrue()
			{
				var a = new Percent(25);
				var b = new Percent(50);

				(a < b).Should().BeTrue();
			}

			[Fact]
			public void LessThan_WhenEqual_ShouldReturnFalse()
			{
				var a = new Percent(50);
				var b = new Percent(50);

				(a < b).Should().BeFalse();
			}

			[Fact]
			public void LessThanOrEqual_WhenLess_ShouldReturnTrue()
			{
				var a = new Percent(25);
				var b = new Percent(50);

				(a <= b).Should().BeTrue();
			}

			[Fact]
			public void LessThanOrEqual_WhenEqual_ShouldReturnTrue()
			{
				var a = new Percent(50);
				var b = new Percent(50);

				(a <= b).Should().BeTrue();
			}

			[Fact]
			public void GreaterThan_WhenGreater_ShouldReturnTrue()
			{
				var a = new Percent(75);
				var b = new Percent(50);

				(a > b).Should().BeTrue();
			}

			[Fact]
			public void GreaterThan_WhenEqual_ShouldReturnFalse()
			{
				var a = new Percent(50);
				var b = new Percent(50);

				(a > b).Should().BeFalse();
			}

			[Fact]
			public void GreaterThanOrEqual_WhenGreater_ShouldReturnTrue()
			{
				var a = new Percent(75);
				var b = new Percent(50);

				(a >= b).Should().BeTrue();
			}

			[Fact]
			public void GreaterThanOrEqual_WhenEqual_ShouldReturnTrue()
			{
				var a = new Percent(50);
				var b = new Percent(50);

				(a >= b).Should().BeTrue();
			}
		}

		public class EqualsMethod
		{
			[Fact]
			public void WhenSameValue_ShouldReturnTrue()
			{
				var a = new Percent(50);
				var b = new Percent(50);

				a.Equals(b).Should().BeTrue();
			}

			[Fact]
			public void WhenDifferentValue_ShouldReturnFalse()
			{
				var a = new Percent(50);
				var b = new Percent(75);

				a.Equals(b).Should().BeFalse();
			}

			[Fact]
			public void WhenObjectIsSameValue_ShouldReturnTrue()
			{
				var a = new Percent(50);
				object b = new Percent(50);

				a.Equals(b).Should().BeTrue();
			}

			[Fact]
			public void WhenObjectIsDifferentType_ShouldReturnFalse()
			{
				var a = new Percent(50);
				object b = 50;

				a.Equals(b).Should().BeFalse();
			}

			[Fact]
			public void WhenObjectIsNull_ShouldReturnFalse()
			{
				var a = new Percent(50);

				a.Equals(null).Should().BeFalse();
			}
		}

		public class GetHashCodeMethod
		{
			[Fact]
			public void WhenSameValue_ShouldReturnSameHashCode()
			{
				var a = new Percent(50);
				var b = new Percent(50);

				a.GetHashCode().Should().Be(b.GetHashCode());
			}

			[Fact]
			public void WhenDifferentValue_ShouldReturnDifferentHashCode()
			{
				var a = new Percent(50);
				var b = new Percent(75);

				a.GetHashCode().Should().NotBe(b.GetHashCode());
			}
		}

		public class CompareToMethod
		{
			[Fact]
			public void WhenLess_ShouldReturnNegative()
			{
				var a = new Percent(25);
				var b = new Percent(50);

				a.CompareTo(b).Should().BeNegative();
			}

			[Fact]
			public void WhenEqual_ShouldReturnZero()
			{
				var a = new Percent(50);
				var b = new Percent(50);

				a.CompareTo(b).Should().Be(0);
			}

			[Fact]
			public void WhenGreater_ShouldReturnPositive()
			{
				var a = new Percent(75);
				var b = new Percent(50);

				a.CompareTo(b).Should().BePositive();
			}
		}

		public class ExplicitCastFromByte
		{
			[Fact]
			public void WhenValidByte_ShouldCreatePercent()
			{
				byte value = 42;

				var p = (Percent)value;

				p.Value.Should().Be(42);
			}

			[Fact]
			public void WhenInvalidByte_ShouldThrow()
			{
				byte value = 101;

				var act = () => (Percent)value;

				act.Should().Throw<ArgumentOutOfRangeException>();
			}
		}

		public class ImplicitCastToByte
		{
			[Fact]
			public void WhenCast_ShouldReturnValue()
			{
				var p = new Percent(42);

				byte value = p;

				value.Should().Be(42);
			}
		}

		public class ToRatio
		{
			[Fact]
			public void WhenCalled_ShouldReturnRatio()
			{
				var p = new Percent(10);

				float r = p.ToRatio();

				r.Should().Be(0.1f);
			}

			[Fact]
			public void WhenZero_ShouldReturnZero()
			{
				float r = Percent.Zero.ToRatio();

				r.Should().Be(0.0f);
			}

			[Fact]
			public void WhenFull_ShouldReturnOne()
			{
				float r = Percent.Full.ToRatio();

				r.Should().Be(1.0f);
			}

			[Theory]
			[InlineData(0, 0.0f)]
			[InlineData(25, 0.25f)]
			[InlineData(50, 0.5f)]
			[InlineData(75, 0.75f)]
			[InlineData(100, 1.0f)]
			public void WhenCalledWithValue_ShouldReturnExpectedRatio(byte value, float expectedRatio)
			{
				var p = new Percent(value);

				p.ToRatio().Should().Be(expectedRatio);
			}
		}

		public class ToStringTests
		{
			[Fact]
			public void WhenCalled_ShouldReturnPercentString()
			{
				var p = new Percent(10);

				p.ToString().Should().Be("10%");
			}

			[Fact]
			public void WhenZero_ShouldReturnZeroPercent()
			{
				Percent.Zero.ToString().Should().Be("0%");
			}

			[Fact]
			public void WhenFull_ShouldReturn100Percent()
			{
				Percent.Full.ToString().Should().Be("100%");
			}
		}

#if NET6_0_OR_GREATER
		public class TryFormat
		{
			[Fact]
			public void WhenBufferSufficient_ShouldFormatAndReturnTrue()
			{
				var p = new Percent(42);
				Span<char> buffer = stackalloc char[4];

				bool result = p.TryFormat(buffer, out int charsWritten, default, null);

				result.Should().BeTrue();
				charsWritten.Should().Be(3);
				buffer[..charsWritten].ToString().Should().Be("42%");
			}

			[Fact]
			public void WhenBufferTooSmall_ShouldReturnFalse()
			{
				var p = new Percent(100);
				Span<char> buffer = stackalloc char[3];

				bool result = p.TryFormat(buffer, out int charsWritten, default, null);

				result.Should().BeFalse();
			}

			[Fact]
			public void WhenFull_ShouldFormat100Percent()
			{
				Span<char> buffer = stackalloc char[4];

				bool result = Percent.Full.TryFormat(buffer, out int charsWritten, default, null);

				result.Should().BeTrue();
				charsWritten.Should().Be(4);
				buffer[..charsWritten].ToString().Should().Be("100%");
			}

			[Fact]
			public void WhenZero_ShouldFormat0Percent()
			{
				Span<char> buffer = stackalloc char[4];

				bool result = Percent.Zero.TryFormat(buffer, out int charsWritten, default, null);

				result.Should().BeTrue();
				charsWritten.Should().Be(2);
				buffer[..charsWritten].ToString().Should().Be("0%");
			}
		}

		public class ToStringWithFormat
		{
			[Fact]
			public void WhenCalled_ShouldReturnPercentString()
			{
				var p = new Percent(42);

				string result = p.ToString(null, null);

				result.Should().Be("42%");
			}
		}
#endif
	}
}
