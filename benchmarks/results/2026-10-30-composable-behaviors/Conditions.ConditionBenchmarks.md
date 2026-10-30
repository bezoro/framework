```

BenchmarkDotNet v0.14.0, Debian GNU/Linux 13 (trixie)
INTEL XEON PLATINUM 8573C, 1 CPU, 5 logical and 5 physical cores
.NET SDK 10.0.203
  [Host]   : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  ShortRun : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

```
| Method                 | Mean      | Error     | StdDev    | Allocated |
|----------------------- |----------:|----------:|----------:|----------:|
| PredicateFastPath      |  1.192 ns | 0.0875 ns | 0.0048 ns |         - |
| AllSixTrue             | 16.644 ns | 2.8439 ns | 0.1559 ns |         - |
| FirstFalseShortCircuit |  4.062 ns | 0.2773 ns | 0.0152 ns |         - |
| ConditionalBranch      |  9.897 ns | 1.0020 ns | 0.0549 ns |         - |
