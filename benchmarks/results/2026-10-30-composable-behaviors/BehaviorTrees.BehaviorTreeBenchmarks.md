```

BenchmarkDotNet v0.14.0, Debian GNU/Linux 13 (trixie)
INTEL XEON PLATINUM 8573C, 1 CPU, 5 logical and 5 physical cores
.NET SDK 10.0.203
  [Host]   : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  ShortRun : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

```
| Method                 | SiblingCount | Mean           | Error         | StdDev      | Allocated |
|----------------------- |------------- |---------------:|--------------:|------------:|----------:|
| **MemoryRunningAction**    | **16**           |       **4.251 ns** |     **1.9825 ns** |   **0.1087 ns** |         **-** |
| ReactivePriorityChecks | 16           |     168.544 ns |    10.7933 ns |   0.5916 ns |         - |
| RestartPair            | 16           |      13.408 ns |     0.1750 ns |   0.0096 ns |         - |
| **MemoryRunningAction**    | **16384**        |       **4.110 ns** |     **0.1072 ns** |   **0.0059 ns** |         **-** |
| ReactivePriorityChecks | 16384        | 148,558.302 ns | 5,711.9691 ns | 313.0923 ns |         - |
| RestartPair            | 16384        |      13.488 ns |     3.2040 ns |   0.1756 ns |         - |
