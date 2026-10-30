```

BenchmarkDotNet v0.14.0, Debian GNU/Linux 13 (trixie)
INTEL XEON PLATINUM 8573C, 1 CPU, 5 logical and 5 physical cores
.NET SDK 10.0.203
  [Host]   : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  ShortRun : .NET 10.0.7 (10.0.726.21808), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=ShortRun  IterationCount=3  LaunchCount=1  
WarmupCount=3  

```
| Method              | Mean     | Error     | StdDev    | Allocated |
|-------------------- |---------:|----------:|----------:|----------:|
| WriteGameplayRecord | 2.749 ns | 0.0883 ns | 0.0048 ns |         - |
| ReadGameplayRecord  | 3.513 ns | 0.7338 ns | 0.0402 ns |         - |
