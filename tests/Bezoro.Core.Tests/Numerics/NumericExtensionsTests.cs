using System;
using System.Numerics;
using Bezoro.Core.Extensions;
using Xunit;

namespace Bezoro.Core.Tests.Numerics;

public sealed class NumericExtensionsTests
{
	[Fact]
	public void IntegerBoundaries_MatchUnboundedArithmetic()
	{
		int[] values = [int.MinValue, int.MinValue + 1, -17, -1, 0, 1, 17, int.MaxValue - 1, int.MaxValue];
		int[] divisors = [1, 2, 3, 7, 1024, int.MaxValue];
		foreach (int value in values)
			foreach (int divisor in divisors)
				AssertInteger(value, divisor);
	}

	[Fact]
	public void LongBoundaries_MatchUnboundedArithmetic()
	{
		long[] values = [long.MinValue, long.MinValue + 1, -17, -1, 0, 1, 17, long.MaxValue - 1, long.MaxValue];
		long[] divisors = [1, 2, 3, 7, 1024, long.MaxValue];
		foreach (long value in values)
			foreach (long divisor in divisors)
				AssertLong(value, divisor);
	}

	[Fact]
	public void SeededIntegerOperations_MatchUnboundedArithmetic()
	{
		var random = new Random(187_439);
		for (int index = 0; index < 20_000; index++)
		{
			AssertInteger((int)random.NextInt64(int.MinValue, (long)int.MaxValue + 1), random.Next(1, int.MaxValue));
			AssertLong(random.NextInt64(long.MinValue, long.MaxValue), random.NextInt64(1, long.MaxValue));
		}
	}

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(int.MinValue)]
	public void IntegerOperations_RejectNonpositiveDivisors(int divisor)
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => 42.FloorDivide(divisor));
		Assert.Throws<ArgumentOutOfRangeException>(() => 42.CeilingDivide(divisor));
		Assert.Throws<ArgumentOutOfRangeException>(() => 42.EuclideanModulo(divisor));
	}

	[Theory]
	[InlineData(0L)]
	[InlineData(-1L)]
	[InlineData(long.MinValue)]
	public void LongOperations_RejectNonpositiveDivisors(long divisor)
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => 42L.FloorDivide(divisor));
		Assert.Throws<ArgumentOutOfRangeException>(() => 42L.CeilingDivide(divisor));
		Assert.Throws<ArgumentOutOfRangeException>(() => 42L.EuclideanModulo(divisor));
	}

	[Fact]
	public void ChunkCoordinates_UseFloorDivisionAndNonnegativeLocalIndices()
	{
		Assert.Equal(-2, (-17).FloorDivide(16));
		Assert.Equal(15, (-17).EuclideanModulo(16));
		Assert.Equal(-1, (-17).CeilingDivide(16));
		Assert.Equal(2, 17.CeilingDivide(16));
	}

	private static void AssertInteger(int value, int divisor)
	{
		var expected = Reference(value, divisor);
		Assert.Equal((int)expected.Floor, value.FloorDivide(divisor));
		Assert.Equal((int)expected.Ceiling, value.CeilingDivide(divisor));
		Assert.Equal((int)expected.Remainder, value.EuclideanModulo(divisor));
	}

	private static void AssertLong(long value, long divisor)
	{
		var expected = Reference(value, divisor);
		Assert.Equal((long)expected.Floor, value.FloorDivide(divisor));
		Assert.Equal((long)expected.Ceiling, value.CeilingDivide(divisor));
		Assert.Equal((long)expected.Remainder, value.EuclideanModulo(divisor));
	}

	private static (BigInteger Floor, BigInteger Ceiling, BigInteger Remainder) Reference(BigInteger value, BigInteger divisor)
	{
		BigInteger remainder = ((value % divisor) + divisor) % divisor;
		BigInteger floor = (value - remainder) / divisor;
		BigInteger ceiling = floor + (remainder.IsZero ? BigInteger.Zero : BigInteger.One);
		return (floor, ceiling, remainder);
	}
}
