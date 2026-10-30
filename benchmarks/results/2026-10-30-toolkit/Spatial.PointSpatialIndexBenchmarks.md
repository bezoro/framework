```

BenchmarkDotNet v0.14.0, Debian GNU/Linux 13 (trixie)
INTEL XEON PLATINUM 8573C, 1 CPU, 5 logical and 5 physical cores
.NET SDK 10.0.203
  [Host]   : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  ShortRun : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method | PointCount | Mean       | Error     | StdDev   | Allocated |
|------- |----------- |-----------:|----------:|---------:|----------:|
| **Circle** | **1024**       |   **110.1 ns** | **207.90 ns** | **11.40 ns** |         **-** |
| **Circle** | **16384**      | **1,288.4 ns** |  **72.19 ns** |  **3.96 ns** |         **-** |
