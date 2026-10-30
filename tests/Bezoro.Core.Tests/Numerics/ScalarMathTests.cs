using System;
using Bezoro.Core.Extensions;
using Bezoro.Core.Numerics;
using Xunit;

namespace Bezoro.Core.Tests.Numerics;

public sealed class ScalarMathTests
{
	[Theory]
	[InlineData(-1d, 0d, 360d, 359d)]
	[InlineData(360d, 0d, 360d, 0d)]
	[InlineData(721d, 0d, 360d, 1d)]
	[InlineData(-721d, 0d, 360d, 359d)]
	[InlineData(181d, -180d, 180d, -179d)]
	[InlineData(-181d, -180d, 180d, 179d)]
	[InlineData(-1d, -0.5d, 0.5d, 0d)]
	public void Wrap_UsesAHalfOpenInterval(double value, double min, double max, double expected)
	{
		Assert.Equal(expected, ScalarMath.Wrap(value, min, max));
		Assert.Equal((float)expected, ScalarMath.Wrap((float)value, (float)min, (float)max));
	}

	[Fact]
	public void Wrap_AvoidsOverflowingTheOffsetAndPreservesInteriorValues()
	{
		double min = -double.MaxValue;
		double max = -double.MaxValue / 2;
		double result = ScalarMath.Wrap(double.MaxValue, min, max);
		Assert.InRange(result, min, max);
		Assert.True(result < max);
		Assert.Equal(-double.MaxValue * 0.75, ScalarMath.Wrap(-double.MaxValue * 0.75, min, max));
		Assert.Equal(0f, ScalarMath.Wrap(0f, -float.MaxValue, float.MaxValue));
		Assert.Equal(-float.MaxValue, ScalarMath.Wrap(float.MaxValue, -float.MaxValue, float.MaxValue));
	}

	[Fact]
	public void Wrap_RoundedUpperBoundaryReturnsTheLowerBound()
	{
		Assert.Equal(0d, ScalarMath.Wrap(-double.Epsilon, 0d, 1d));
		Assert.Equal(0f, ScalarMath.Wrap(-float.Epsilon, 0f, 1f));
	}

	[Fact]
	public void Wrap_RejectsInvalidAndUnrepresentableIntervals()
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => ScalarMath.Wrap(0d, 1d, 1d));
		Assert.Throws<ArgumentOutOfRangeException>(() => ScalarMath.Wrap(0d, 2d, 1d));
		Assert.Throws<ArgumentOutOfRangeException>(() => ScalarMath.Wrap(0d, -double.MaxValue, double.MaxValue));
		Assert.Throws<ArgumentOutOfRangeException>(() => ScalarMath.Wrap(0f, 1f, 1f));
	}

	[Fact]
	public void Wrap_IntegerFixturesMatchExactDecimalModulo()
	{
		var random = new Random(96_647);
		for (int index = 0; index < 10_000; index++)
		{
			double value = random.NextInt64(-1_000_000_000_000, 1_000_000_000_000);
			double min = random.Next(-1_000_000, 1_000_000);
			double width = random.Next(1, 1_000_000);
			decimal remainder = ((decimal)value - (decimal)min) % (decimal)width;
			if (remainder < 0) remainder += (decimal)width;
			Assert.Equal((double)((decimal)min + remainder), ScalarMath.Wrap(value, min, min + width));
		}
	}

	[Theory]
	[InlineData(0d, 10d, 3d, 3d)]
	[InlineData(10d, 0d, 3d, 7d)]
	[InlineData(0d, 2d, 3d, 2d)]
	[InlineData(2d, 0d, 3d, 0d)]
	[InlineData(3d, 3d, 5d, 3d)]
	[InlineData(3d, 4d, 0d, 3d)]
	public void MoveTowards_LimitsMovementAndDoesNotOvershoot(double current, double target, double delta, double expected)
	{
		Assert.Equal(expected, ScalarMath.MoveTowards(current, target, delta));
		Assert.Equal((float)expected, ScalarMath.MoveTowards((float)current, (float)target, (float)delta));
	}

	[Fact]
	public void MoveTowards_HandlesOppositeFiniteExtremes()
	{
		Assert.Equal(0d, ScalarMath.MoveTowards(-double.MaxValue, double.MaxValue, double.MaxValue));
		Assert.Equal(0d, ScalarMath.MoveTowards(double.MaxValue, -double.MaxValue, double.MaxValue));
		Assert.Equal(0f, ScalarMath.MoveTowards(-float.MaxValue, float.MaxValue, float.MaxValue));
		Assert.Equal(double.MaxValue, ScalarMath.MoveTowards(0d, double.MaxValue, double.MaxValue));
		Assert.Throws<ArgumentOutOfRangeException>(() => ScalarMath.MoveTowards(0d, 1d, -1d));
	}

	[Fact]
	public void LerpClamped_HandlesExtremesAndPreservesExactEndpoints()
	{
		Assert.Equal(0d, ScalarMath.LerpClamped(-double.MaxValue, double.MaxValue, 0.5d));
		Assert.Equal(0f, ScalarMath.LerpClamped(-float.MaxValue, float.MaxValue, 0.5f));
		Assert.Equal(double.MaxValue, ScalarMath.LerpClamped(double.MaxValue, double.MaxValue, 0.5d));
		Assert.Equal(-double.MaxValue, ScalarMath.LerpClamped(-double.MaxValue, double.MaxValue, -1d));
		Assert.Equal(double.MaxValue, ScalarMath.LerpClamped(-double.MaxValue, double.MaxValue, 2d));
		Assert.Equal(5d, ScalarMath.LerpClamped(10d, 0d, 0.5d));
		Assert.Equal(double.Epsilon, ScalarMath.LerpClamped(double.Epsilon, 1d, 0d));
	}

	[Fact]
	public void InverseLerpClamped_SupportsReversedAndExtremeIntervals()
	{
		Assert.Equal(0.5d, ScalarMath.InverseLerpClamped(0d, -double.MaxValue, double.MaxValue));
		Assert.Equal(0.25d, ScalarMath.InverseLerpClamped(double.MaxValue / 2, double.MaxValue, -double.MaxValue));
		Assert.Equal(0.5f, ScalarMath.InverseLerpClamped(0f, -float.MaxValue, float.MaxValue));
		Assert.Equal(0d, ScalarMath.InverseLerpClamped(11d, 10d, 0d));
		Assert.Equal(1d, ScalarMath.InverseLerpClamped(-1d, 10d, 0d));
		Assert.Equal(0.5d, ScalarMath.InverseLerpClamped(0d, -double.Epsilon, double.Epsilon));
		Assert.Throws<ArgumentException>(() => ScalarMath.InverseLerpClamped(3d, 2d, 2d));
		Assert.Throws<ArgumentException>(() => ScalarMath.InverseLerpClamped(3f, 2f, 2f));
	}

	[Fact]
	public void RemapClamped_MapsAndClampsUsingTheSourceDirection()
	{
		Assert.Equal(25d, ScalarMath.RemapClamped(7.5d, 10d, 0d, 0d, 100d));
		Assert.Equal(100d, ScalarMath.RemapClamped(-1d, 10d, 0d, 0d, 100d));
		Assert.Equal(0d, ScalarMath.RemapClamped(0d, -double.MaxValue, double.MaxValue, -double.MaxValue, double.MaxValue));
		Assert.Equal(75f, ScalarMath.RemapClamped(0.25f, 0f, 1f, 100f, 0f));
		Assert.Equal(12d, ScalarMath.RemapClamped(5d, 0d, 10d, 12d, 12d));
		Assert.Throws<ArgumentException>(() => ScalarMath.RemapClamped(5d, 1d, 1d, 0d, 10d));
		Assert.Throws<ArgumentException>(() => ScalarMath.RemapClamped(5f, 1f, 1f, 0f, 10f));
	}

	[Fact]
	public void Interpolation_StaysWithinFiniteEndpointsForRandomFiniteBitPatterns()
	{
		var random = new Random(883_167);
		for (int index = 0; index < 20_000; index++)
		{
			double from = RandomFinite(random);
			double to = RandomFinite(random);
			double amount = random.NextDouble();
			double actual = ScalarMath.LerpClamped(from, to, amount);
			Assert.True(double.IsFinite(actual));
			Assert.InRange(actual, Math.Min(from, to), Math.Max(from, to));
			if (from == to) continue;
			double inverse = ScalarMath.InverseLerpClamped(actual, from, to);
			Assert.InRange(inverse, 0d, 1d);
			// Quantization can collapse distinct fractions when one endpoint dominates.
			if (Math.Abs(from) is > 1e-100 and < 1e100 && Math.Abs(to) is > 1e-100 and < 1e100)
				Assert.InRange(Math.Abs(inverse - amount), 0, 1e-12);
		}
	}

	[Fact]
	public void Damp_UsesRateTimesElapsedAndIsIndependentOfFramePartitionWithinRounding()
	{
		double expected = 100 * (1 - Math.Exp(-3d * 2d));
		double single = ScalarMath.Damp(0d, 100d, 3d, 2d);
		double split = 0;
		for (int frame = 0; frame < 120; frame++) split = ScalarMath.Damp(split, 100d, 3d, 2d / 120);
		Assert.InRange(Math.Abs(single - expected), 0, 1e-12);
		Assert.InRange(Math.Abs(single - split), 0, 1e-12);
		Assert.Equal(7d, ScalarMath.Damp(7d, 20d, 0d, 4d));
		Assert.Equal(7d, ScalarMath.Damp(7d, 20d, 4d, 0d));
		Assert.Equal(7f, ScalarMath.Damp(7f, 20f, 4f, 0f));
		Assert.Equal(20d, ScalarMath.Damp(7d, 20d, double.MaxValue, double.MaxValue));
	}

	[Fact]
	public void Damp_PreservesVerySmallContributionsWithoutSubtractiveCancellation()
	{
		double result = ScalarMath.Damp(0d, 1e300, 1d, 1e-20);
		Assert.InRange(result, 1e280 * (1 - 1e-14), 1e280 * (1 + 1e-14));
		Assert.InRange(Math.Abs(ScalarMath.Damp(-double.MaxValue, double.MaxValue, Math.Log(2), 1d)),
			0d, double.MaxValue * 1e-15);
		Assert.Throws<ArgumentOutOfRangeException>(() => ScalarMath.Damp(0d, 1d, -1d, 1d));
		Assert.Throws<ArgumentOutOfRangeException>(() => ScalarMath.Damp(0d, 1d, 1d, -1d));
		Assert.Throws<ArgumentOutOfRangeException>(() => ScalarMath.Damp(0f, 1f, -1f, 1f));
		Assert.Throws<ArgumentOutOfRangeException>(() => ScalarMath.Damp(0f, 1f, 1f, -1f));
	}

	[Fact]
	public void Damp_PreservesLateStageDecayAndMatchesPartitionedTime()
	{
		double expected = Math.Exp(-40d);
		double single = ScalarMath.Damp(1d, 0d, 1d, 40d);
		double split = 1;
		for (int step = 0; step < 40; step++) split = ScalarMath.Damp(split, 0d, 1d, 1d);
		Assert.Equal(expected, single);
		Assert.InRange(Math.Abs(single - split), 0d, expected * 1e-13);
		Assert.Equal(-expected, ScalarMath.Damp(-1d, 0d, 1d, 40d));
		Assert.Equal((float)expected, ScalarMath.Damp(1f, 0f, 1f, 40f));
	}

	[Theory]
	[InlineData(744d)]
	[InlineData(800d)]
	public void Damp_RescalesSubnormalDecayToRetainRepresentableEndpointContributions(double elapsedTime)
	{
		double single = ScalarMath.Damp(double.MaxValue, 0d, 1d, elapsedTime);
		double midpoint = ScalarMath.Damp(double.MaxValue, 0d, 1d, elapsedTime * 0.5);
		double split = ScalarMath.Damp(midpoint, 0d, 1d, elapsedTime * 0.5);
		double expected = Math.Exp(Math.Log(double.MaxValue) - elapsedTime);
		Assert.True(single > 0 && double.IsFinite(single));
		Assert.Equal(split, single);
		Assert.InRange(Math.Abs(single - expected), 0d, expected * 1e-12);
		Assert.Equal(-single, ScalarMath.Damp(-double.MaxValue, 0d, 1d, elapsedTime));
	}

	[Fact]
	public void Damp_RescalesAnUnderflowingRateTimeProductBeforeApplyingIt()
	{
		double expected = Math.ScaleB(double.MaxValue, -1075);
		double single = ScalarMath.Damp(0d, double.MaxValue, double.Epsilon, 0.5d);
		Assert.True(single > 0 && double.IsFinite(single));
		Assert.Equal(expected, single);
		Assert.Equal(single, ScalarMath.Damp(0d, double.MaxValue, 0.5d, double.Epsilon));
		Assert.Equal(-single, ScalarMath.Damp(0d, -double.MaxValue, double.Epsilon, 0.5d));
		Assert.Equal(1d + expected, ScalarMath.Damp(1d, double.MaxValue, double.Epsilon, 0.5d));
		Assert.Equal(double.MaxValue,
			ScalarMath.Damp(double.MaxValue, -double.MaxValue, double.Epsilon, 0.5d));
	}

	[Fact]
	public void Damp_RescalesSubnormalRateTimeProductsWithoutPrematureRoundingOrEndpointOverflow()
	{
		double expected = Math.ScaleB(double.MaxValue, -1074) * 1.1d;
		Assert.Equal(expected, ScalarMath.Damp(0d, double.MaxValue, double.Epsilon, 1.1d));
		Assert.Equal(expected, ScalarMath.Damp(0d, double.MaxValue, 1.1d, double.Epsilon));
		// A small endpoint needs the larger factor first to preserve its final subnormal result.
		Assert.Equal(Math.ScaleB(100_000d, -1074), ScalarMath.Damp(0d, 1e-10, 1e15, double.Epsilon));
	}

	[Fact]
	public void Damp_ScalesTheSmallerFactorFirstAtTheRoundedOverflowThreshold()
	{
		const double factor = 103.53421365645285;
		double endpoint = double.MaxValue / factor;
		Assert.True(double.IsPositiveInfinity(endpoint * factor));
		double expected = endpoint * double.Epsilon * factor;
		Assert.Equal(expected, ScalarMath.Damp(0d, endpoint, factor, double.Epsilon));
	}

	[Theory]
	[InlineData(double.NaN)]
	[InlineData(double.PositiveInfinity)]
	[InlineData(double.NegativeInfinity)]
	public void DoubleOperations_RejectEveryNonfiniteInput(double invalid)
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => ScalarMath.Wrap(invalid, 0d, 1d));
		Assert.Throws<ArgumentOutOfRangeException>(() => ScalarMath.Wrap(0d, invalid, 1d));
		Assert.Throws<ArgumentOutOfRangeException>(() => ScalarMath.Wrap(0d, 0d, invalid));
		Assert.Throws<ArgumentOutOfRangeException>(() => ScalarMath.MoveTowards(invalid, 1d, 1d));
		Assert.Throws<ArgumentOutOfRangeException>(() => ScalarMath.MoveTowards(0d, invalid, 1d));
		Assert.Throws<ArgumentOutOfRangeException>(() => ScalarMath.MoveTowards(0d, 1d, invalid));
		Assert.Throws<ArgumentOutOfRangeException>(() => ScalarMath.LerpClamped(invalid, 1d, 0.5d));
		Assert.Throws<ArgumentOutOfRangeException>(() => ScalarMath.LerpClamped(0d, invalid, 0.5d));
		Assert.Throws<ArgumentOutOfRangeException>(() => ScalarMath.LerpClamped(0d, 1d, invalid));
		Assert.Throws<ArgumentOutOfRangeException>(() => ScalarMath.InverseLerpClamped(invalid, 0d, 1d));
		Assert.Throws<ArgumentOutOfRangeException>(() => ScalarMath.InverseLerpClamped(0d, invalid, 1d));
		Assert.Throws<ArgumentOutOfRangeException>(() => ScalarMath.InverseLerpClamped(0d, 0d, invalid));
		Assert.Throws<ArgumentOutOfRangeException>(() => ScalarMath.RemapClamped(invalid, 0d, 1d, 0d, 1d));
		Assert.Throws<ArgumentOutOfRangeException>(() => ScalarMath.RemapClamped(0d, invalid, 1d, 0d, 1d));
		Assert.Throws<ArgumentOutOfRangeException>(() => ScalarMath.RemapClamped(0d, 0d, invalid, 0d, 1d));
		Assert.Throws<ArgumentOutOfRangeException>(() => ScalarMath.RemapClamped(0d, 0d, 1d, invalid, 1d));
		Assert.Throws<ArgumentOutOfRangeException>(() => ScalarMath.RemapClamped(0d, 0d, 1d, 0d, invalid));
		Assert.Throws<ArgumentOutOfRangeException>(() => ScalarMath.Damp(invalid, 1d, 1d, 1d));
		Assert.Throws<ArgumentOutOfRangeException>(() => ScalarMath.Damp(0d, invalid, 1d, 1d));
		Assert.Throws<ArgumentOutOfRangeException>(() => ScalarMath.Damp(0d, 1d, invalid, 1d));
		Assert.Throws<ArgumentOutOfRangeException>(() => ScalarMath.Damp(0d, 1d, 1d, invalid));
	}

	[Theory]
	[InlineData(float.NaN)]
	[InlineData(float.PositiveInfinity)]
	[InlineData(float.NegativeInfinity)]
	public void FloatOperations_RejectNonfiniteInputs(float invalid)
	{
		Assert.Throws<ArgumentOutOfRangeException>(() => ScalarMath.Wrap(invalid, 0f, 1f));
		Assert.Throws<ArgumentOutOfRangeException>(() => ScalarMath.MoveTowards(0f, invalid, 1f));
		Assert.Throws<ArgumentOutOfRangeException>(() => ScalarMath.LerpClamped(0f, 1f, invalid));
		Assert.Throws<ArgumentOutOfRangeException>(() => ScalarMath.InverseLerpClamped(0f, invalid, 1f));
		Assert.Throws<ArgumentOutOfRangeException>(() => ScalarMath.RemapClamped(0f, 0f, 1f, invalid, 1f));
		Assert.Throws<ArgumentOutOfRangeException>(() => ScalarMath.Damp(0f, 1f, invalid, 1f));
	}

	[Fact]
	public void NumericWorkload_AllocatesNoManagedMemoryAfterWarmup()
	{
		RunNumericWorkload(2000);
		long before = GC.GetAllocatedBytesForCurrentThread();
		double value = RunNumericWorkload(2000);
		long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
		Assert.True(double.IsFinite(value));
		Assert.Equal(0, allocated);
	}

	private static double RunNumericWorkload(int count)
	{
		double sum = 0;
		for (int index = 1; index <= count; index++)
		{
			sum += (-index).FloorDivide(16) + index.CeilingDivide(17) + (-index).EuclideanModulo(19);
			sum += (-(long)index).FloorDivide(16) + ((long)index).CeilingDivide(17) + (-(long)index).EuclideanModulo(19);
			sum += ScalarMath.Wrap((double)index, -180d, 180d);
			sum += ScalarMath.MoveTowards((double)index, 42d, 0.1d);
			sum += ScalarMath.LerpClamped(-10d, 10d, (index % 101) / 100d);
			sum += ScalarMath.InverseLerpClamped(index, 0d, 2000d);
			sum += ScalarMath.RemapClamped(index, 0d, 2000d, 10d, -10d);
			sum += ScalarMath.Damp(index, 42d, 3d, 0.016d);
		}
		return sum;
	}

	private static double RandomFinite(Random random)
	{
		while (true)
		{
			double value = BitConverter.Int64BitsToDouble(random.NextInt64(long.MinValue, long.MaxValue));
			if (double.IsFinite(value)) return value;
		}
	}
}
