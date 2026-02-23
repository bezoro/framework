using System;
using System.Numerics;
using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(UIntVector2))]
public class UIntVector2FromVector2Tests
{
	[Fact]
	public void WhenInfinity_WhenCalled_ShouldThrow()
	{
		var v = new Vector2(float.PositiveInfinity, 0f);

		Action act = () => UIntVector2.FromVector2(v);

		act.Should().Throw<ArgumentOutOfRangeException>();
	}

	[Fact]
	public void WhenNaN_WhenCalled_ShouldThrow()
	{
		var v = new Vector2(float.NaN, 0f);

		Action act = () => UIntVector2.FromVector2(v);

		act.Should().Throw<ArgumentOutOfRangeException>();
	}

	[Theory]
	[InlineData(-1f,            0f)]
	[InlineData(0f,             -0.1f)]
	[InlineData(5_000_000_000f, 0f)] // larger than uint.MaxValue
	public void WhenNegativeOrTooLarge_WhenCalled_ShouldThrow(float x, float y)
	{
		var v = new Vector2(x, y);

		Action act = () => UIntVector2.FromVector2(v);

		act.Should().Throw<ArgumentOutOfRangeException>();
	}

	[Fact]
	public void WhenNonWholeBeyondTolerance_WhenCalled_ShouldThrow()
	{
		var v = new Vector2(1.000002f, 2f);

		Action act = () => UIntVector2.FromVector2(v);

		act.Should().Throw<ArgumentOutOfRangeException>();
	}

	[Fact]
	public void WhenValidVector2_WhenCalled_ShouldReturnNewValidUIntVector2()
	{
		var v = new Vector2(3f, 7f);

		var u = UIntVector2.FromVector2(v);

		u.X.Should().Be(3u);
		u.Y.Should().Be(7u);
	}

	[Fact]
	public void WhenValidVector2_WhenCalled_ShouldRoundToNearestInteger()
	{
		// Differences within 1e-6 should be accepted and rounded
		var v = new Vector2(2.0000004f, 5.9999996f);

		var u = UIntVector2.FromVector2(v);

		u.X.Should().Be(2u);
		u.Y.Should().Be(6u);
	}
}
