```

BenchmarkDotNet v0.14.0, Debian GNU/Linux 13 (trixie)
INTEL XEON PLATINUM 8573C, 1 CPU, 5 logical and 5 physical cores
.NET SDK 10.0.203
  [Host]   : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  ShortRun : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

```
| Method          | Depth | Mean      | Error     | StdDev   | Allocated |
|---------------- |------ |----------:|----------:|---------:|----------:|
| **NestedBudgetOne** | **1**     |  **11.42 ns** |  **0.516 ns** | **0.028 ns** |         **-** |
| NestedFullPath  | 1     |  11.06 ns |  0.165 ns | 0.009 ns |         - |
| **NestedBudgetOne** | **8**     |  **49.06 ns** |  **1.261 ns** | **0.069 ns** |         **-** |
| NestedFullPath  | 8     | 116.11 ns | 12.672 ns | 0.695 ns |         - |
| **NestedBudgetOne** | **32**    | **156.78 ns** | **28.985 ns** | **1.589 ns** |         **-** |
| NestedFullPath  | 32    | 505.00 ns | 60.255 ns | 3.303 ns |         - |
