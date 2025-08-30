using System;
using System.Numerics;
using Bezoro.Core.Common.Primitives;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Common.Primitives;

[TestSubject(typeof(UIntVector2))]
public static class UIntVector2Tests
{
	public class Unit
	{
		[Fact]
		public void ExplicitConversion_FromVector2_ShouldSucceed_WhenValid()
		{
			var v = new Vector2(4f, 2f);

			var u = (UIntVector2)v;

			u.Should().Be(new UIntVector2(4u, 2u));
		}

		[Fact]
		public void ExplicitConversion_FromVector2_ShouldThrow_WhenInvalid()
		{
			var v = new Vector2(1.5f, 2f);

			var act = () =>
			{
				var _ = (UIntVector2)v;
			};

			act.Should().Throw<ArgumentOutOfRangeException>();
		}

		[Fact]
		public void FromVector2_ShouldConvert_WhenWholeNonNegativeWithinRange()
		{
			var v = new Vector2(3f, 7f);

			var u = UIntVector2.FromVector2(v);

			u.X.Should().Be(3u);
			u.Y.Should().Be(7u);
		}

		[Fact]
		public void FromVector2_ShouldRoundWithinTolerance()
		{
			// Differences within 1e-6 should be accepted and rounded
			var v = new Vector2(2.0000004f, 5.9999996f);

			var u = UIntVector2.FromVector2(v);

			u.X.Should().Be(2u);
			u.Y.Should().Be(6u);
		}

		[Fact]
		public void FromVector2_ShouldThrow_WhenInfinity()
		{
			var v = new Vector2(float.PositiveInfinity, 0f);

			Action act = () => UIntVector2.FromVector2(v);

			act.Should().Throw<ArgumentOutOfRangeException>();
		}

		[Fact]
		public void FromVector2_ShouldThrow_WhenNaN()
		{
			var v = new Vector2(float.NaN, 0f);

			Action act = () => UIntVector2.FromVector2(v);

			act.Should().Throw<ArgumentOutOfRangeException>();
		}

		[Theory]
		[InlineData(-1f,            0f)]
		[InlineData(0f,             -0.1f)]
		[InlineData(5_000_000_000f, 0f)] // larger than uint.MaxValue
		public void FromVector2_ShouldThrow_WhenNegativeOrTooLarge(float x, float y)
		{
			var v = new Vector2(x, y);

			Action act = () => UIntVector2.FromVector2(v);

			act.Should().Throw<ArgumentOutOfRangeException>();
		}

		[Fact]
		public void FromVector2_ShouldThrow_WhenNonWholeBeyondTolerance()
		{
			var v = new Vector2(1.000002f, 2f);

			Action act = () => UIntVector2.FromVector2(v);

			act.Should().Throw<ArgumentOutOfRangeException>();
		}

		[Fact]
		public void ImplicitConversion_ToVector2_ShouldProduceSameComponents()
		{
			var u = new UIntVector2(12u, 34u);

			Vector2 v = u;

			v.X.Should().Be(12f);
			v.Y.Should().Be(34f);
		}

		[Fact]
		public void ToString_ShouldFormatAsExpected()
		{
			var u = new UIntVector2(3u, 4u);

			u.ToString().Should().Be("(3, 4)");
		}

		[Fact]
		public void ToVector2_ShouldProduceSameComponents()
		{
			var u = new UIntVector2(5u, 6u);

			var v = u.ToVector2();

			v.Should().Be(new Vector2(5f, 6f));
		}

		[Fact]
		public void TryFromVector2_ShouldReturnFalseAndNull_WhenInvalid()
		{
			var v = new Vector2(-1f, 0f);

			bool ok = UIntVector2.TryFromVector2(v, out var result);

			ok.Should().BeFalse();
			result.Should().Be(null);
		}

		[Fact]
		public void TryFromVector2_ShouldReturnTrueAndResult_WhenValid()
		{
			var v = new Vector2(8f, 9f);

			bool ok = UIntVector2.TryFromVector2(v, out var result);

			ok.Should().BeTrue();
			result.Should().Be(new UIntVector2(8u, 9u));
		}
	}
}
