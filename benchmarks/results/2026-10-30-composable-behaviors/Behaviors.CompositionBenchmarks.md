```

BenchmarkDotNet v0.14.0, Debian GNU/Linux 13 (trixie)
INTEL XEON PLATINUM 8573C, 1 CPU, 5 logical and 5 physical cores
.NET SDK 10.0.203
  [Host]   : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  ShortRun : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

```
| Method          | Depth | Mean      | Error      | StdDev    | Allocated |
|---------------- |------ |----------:|-----------:|----------:|----------:|
| **NestedBudgetOne** | **1**     |  **11.79 ns** |   **0.464 ns** |  **0.025 ns** |         **-** |
| NestedFullPath  | 1     |  13.86 ns |  12.158 ns |  0.666 ns |         - |
| **NestedBudgetOne** | **8**     |  **59.36 ns** |  **40.550 ns** |  **2.223 ns** |         **-** |
| NestedFullPath  | 8     | 139.10 ns | 376.727 ns | 20.650 ns |         - |
| **NestedBudgetOne** | **32**    | **157.77 ns** |  **10.962 ns** |  **0.601 ns** |         **-** |
| NestedFullPath  | 32    | 482.93 ns |  49.275 ns |  2.701 ns |         - |
