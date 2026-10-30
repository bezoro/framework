```

BenchmarkDotNet v0.14.0, Debian GNU/Linux 13 (trixie)
INTEL XEON PLATINUM 8573C, 1 CPU, 5 logical and 5 physical cores
.NET SDK 10.0.203
  [Host]   : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  ShortRun : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

```
| Method          | DeclaredStates | Mean      | Error     | StdDev    | Allocated |
|---------------- |--------------- |----------:|----------:|----------:|----------:|
| **UnmatchedSignal** | **2**              |  **4.260 ns** | **0.2610 ns** | **0.0143 ns** |         **-** |
| Transition      | 2              | 26.843 ns | 5.0519 ns | 0.2769 ns |         - |
| **UnmatchedSignal** | **100000**         |  **4.231 ns** | **0.5870 ns** | **0.0322 ns** |         **-** |
| Transition      | 100000         | 26.852 ns | 0.4402 ns | 0.0241 ns |         - |
