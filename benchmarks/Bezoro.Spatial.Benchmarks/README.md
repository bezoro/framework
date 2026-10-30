# Spatial benchmarks

The workloads use a uniform point distribution in a 128 by 128 world, four-unit cells, and either 1,024 or 16,384 points. Setup constructs the index and allocates result buffers. Measurements cover local circle/rectangle queries, exact required counts for an undersized destination, and moves across cells. The benchmark classes remain inheritable for BenchmarkDotNet's generated runner.

```sh
dotnet run -c Release --project benchmarks/Bezoro.Spatial.Benchmarks -- --filter '*PointSpatialIndexBenchmarks*'
```

`MemoryDiagnoser` measures steady-state managed allocation. These workloads do not establish Unity, IL2CPP, or Godot performance. Cell density, queried area, payload size, and the required count pass affect latency. The test suite separately checks zero allocation after warmup; no timing result is claimed until a benchmark report is recorded.

## Recorded toolkit diagnostic

[Selected measurements and raw reports](../results/2026-10-30-toolkit/README.md) record the source revision, runtime, workload parameters and error intervals. Only the listed workloads were measured; the project also defines other profiling cases.
