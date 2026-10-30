using BenchmarkDotNet.Attributes;
using Bezoro.Core.Extensions;
using Bezoro.Core.Numerics;

namespace Bezoro.Core.Benchmarks;

[MemoryDiagnoser]
public class NumericUtilitiesBenchmarks
{
	private long _coordinate = -1_000_001;
	private double _value = -721.25;
	// Runtime inputs keep the damping benchmark from folding Exp(-rate * elapsedTime) into a constant.
	private double _target = 42;
	private double _rate = 3;
	private double _elapsedTime = 0.016;

	[Benchmark]
	public long NegativeChunkCoordinate() => _coordinate.FloorDivide(16);

	[Benchmark]
	public long PositiveLocalIndex() => _coordinate.EuclideanModulo(16);

	[Benchmark]
	public double WrapDegrees() => ScalarMath.Wrap(_value, -180d, 180d);

	[Benchmark]
	public double InterpolateExtremeEndpoints() => ScalarMath.LerpClamped(-double.MaxValue, double.MaxValue, 0.75d);

	[Benchmark]
	public double ExponentialDamping() => ScalarMath.Damp(_value, _target, _rate, _elapsedTime);
}
