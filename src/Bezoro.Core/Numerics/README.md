# Numeric utilities

These pure operations keep numeric policies explicit. They require no engine types or shared mutable state and allocate no managed storage on valid calls.

## Chunk coordinates and cyclic indices

```csharp
using Bezoro.Core.Extensions;

int chunk = (-17).FloorDivide(16);       // -2
int local = (-17).EuclideanModulo(16);   // 15
int chunksNeeded = 17.CeilingDivide(16); // 2
```

`FloorDivide`, `CeilingDivide`, and `EuclideanModulo` support both `int` and `long`. The divisor or modulus must be positive. Zero and negative values throw `ArgumentOutOfRangeException`. Every numerator is supported, including `int.MinValue` and `long.MinValue`. No method negates the numerator or adds a rounding offset to it, so signed extremes do not overflow. Remainders are in `[0, modulus)`.

## Scalars

```csharp
using Bezoro.Core.Numerics;

double heading = ScalarMath.Wrap(-181d, -180d, 180d); // 179
float position = ScalarMath.MoveTowards(current, target, speed * elapsed);
float opacity = ScalarMath.Damp(currentOpacity, targetOpacity,
    rate: 3f, elapsedTime: elapsed);
double healthBar = ScalarMath.RemapClamped(health, 0d, maximumHealth, 0d, 1d);
```

Every operation has `float` and `double` overloads. All arguments must be finite; NaN and either infinity throw `ArgumentOutOfRangeException`. Float overloads use double intermediates to avoid float overflow before a bounded result is computed.

| Operation | Policy |
| --- | --- |
| `Wrap(value, min, max)` | Returns a value in `[min, max)`. The interval must be increasing. Double intervals also need a finite representable width; `[-double.MaxValue, double.MaxValue)` is rejected. Already-contained values are preserved. Both operands are reduced before subtracting to avoid overflowing `value - min`. Rounding to the excluded upper boundary maps to `min`. |
| `MoveTowards(current, target, maxDelta)` | Moves by at most the finite nonnegative delta, stops at the target, and permits opposite-sign finite extremes. Zero delta preserves current. |
| `LerpClamped(from, to, amount)` | Clamps the finite amount to `[0, 1]`. It preserves exact endpoints and supports opposite-sign extremes without overflowing their difference. |
| `InverseLerpClamped(value, from, to)` | Returns a fraction in `[0, 1]`. Reversed intervals are supported. Equal endpoints throw `ArgumentException`; very small nonzero intervals are valid. |
| `RemapClamped(value, fromMin, fromMax, toMin, toMax)` | Combines clamped inverse interpolation and interpolation. Either interval may be reversed; equal output endpoints are valid. Equal source endpoints throw `ArgumentException`. |
| `Damp(current, target, rate, elapsedTime)` | Interpolates by `1 - exp(-rate * elapsedTime)`. Rate and elapsed time must be finite and nonnegative. Rate is convergence per unit of elapsed time, so use seconds consistently for a per-second rate. Zero rate or elapsed time preserves current. An overflowing product reaches target. A series preserves small contributions that `1 - exp(-x)` would otherwise cancel. |

For constant target and rate, exponential damping follows the same mathematical trajectory when elapsed time is split across frames. Floating-point rounding prevents exact bitwise equivalence between partitions. The runtime's exponential implementation and processor arithmetic can also differ. These helpers do not establish deterministic replay across different numeric runtimes.

Wrapping very large values into narrow intervals cannot restore precision already lost in the input. Interpolation likewise cannot represent changes below the output's spacing. Profile the selected engine/player runtime if those boundaries matter.

The older `FloatExtensions.Map` is an unclamped mapping method with its own source-width tolerance. `RemapClamped` has a distinct contract and accepts any distinct finite source endpoints, including subnormal widths.

## Validation

Tests compare integer operations with unbounded `BigInteger` arithmetic, wrap integer fixtures with decimal modulo, exercise floating bit-pattern boundaries, validate invalid inputs and damping partitions, and measure a warmed allocation-free workload. The numeric benchmarks are part of the Core benchmark project. No latency result is implied by their presence.

```sh
dotnet test tests/Bezoro.Core.Tests/Bezoro.Core.Tests.csproj -c Release --filter FullyQualifiedName~Numerics
dotnet run -c Release --project benchmarks/Bezoro.Core.Benchmarks -- --filter '*NumericUtilitiesBenchmarks*'
```
