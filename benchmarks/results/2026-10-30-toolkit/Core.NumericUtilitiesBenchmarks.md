```

BenchmarkDotNet v0.14.0, Debian GNU/Linux 13 (trixie)
INTEL XEON PLATINUM 8573C, 1 CPU, 5 logical and 5 physical cores
.NET SDK 10.0.203
  [Host]   : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  ShortRun : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

```
| Method                  | Mean      | Error     | StdDev    | Allocated |
|------------------------ |----------:|----------:|----------:|----------:|
| NegativeChunkCoordinate | 0.2870 ns | 0.0498 ns | 0.0027 ns |         - |
| ExponentialDamping      | 8.0226 ns | 0.0735 ns | 0.0040 ns |         - |
