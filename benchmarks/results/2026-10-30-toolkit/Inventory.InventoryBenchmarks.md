```

BenchmarkDotNet v0.14.0, Debian GNU/Linux 13 (trixie)
INTEL XEON PLATINUM 8573C, 1 CPU, 5 logical and 5 physical cores
.NET SDK 10.0.203
  [Host]   : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  ShortRun : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=ShortRun  IterationCount=3  LaunchCount=1
WarmupCount=3

```
| Method            | Slots | Mean      | Error    | StdDev   | Allocated |
|------------------ |------ |----------:|---------:|---------:|----------:|
| **TransferRoundTrip** | **16**    |  **55.14 ns** | **20.26 ns** | **1.110 ns** |         **-** |
| **TransferRoundTrip** | **128**   | **345.19 ns** | **21.59 ns** | **1.183 ns** |         **-** |
