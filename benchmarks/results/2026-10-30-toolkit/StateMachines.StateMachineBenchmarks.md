```

BenchmarkDotNet v0.14.0, Debian GNU/Linux 13 (trixie)
INTEL XEON PLATINUM 8573C, 1 CPU, 5 logical and 5 physical cores
.NET SDK 10.0.203
  [Host]   : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  ShortRun : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method      | TransitionCount | Mean      | Error     | StdDev    | Allocated |
|------------ |---------------- |----------:|----------:|----------:|----------:|
| **MatchedStep** | **16**              |  **7.393 ns** | **0.9807 ns** | **0.0538 ns** |         **-** |
| **MatchedStep** | **1024**            | **18.728 ns** | **0.2568 ns** | **0.0141 ns** |         **-** |
| **MatchedStep** | **65536**           | **21.391 ns** | **1.3802 ns** | **0.0757 ns** |         **-** |
