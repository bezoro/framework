# Bezoro.ECS.Benchmarks

BenchmarkDotNet suite for core ECS performance targets.

## Benchmarks

| Benchmark Type | Description |
|---|---|
| `EcsHotPathBenchmarks` | Iteration throughput, parallel chunk iteration, entity lookup, query cache-hit path, and wildcard relationship queries. |
| `EcsCommandBufferBenchmarks` | Command recording/playback throughput for deferred component updates. |
| `EcsStructuralBenchmarks` | Entity create/destroy throughput and structural add-component costs. |
| `EcsMemoryChurnBenchmarks` | Managed-reference component churn (spawn+despawn) to measure allocation pressure and retention behavior. |
| `EcsCommandBufferBurstBenchmarks` | Large deferred creation bursts to benchmark record/playback and post-playback memory behavior. |
| `EcsV2CommandStreamBurstBenchmarks` | WorldV2 fixed-capacity command-stream creation bursts with playback/reset reuse behavior. |
| `EcsV2CommandStreamSetBurstBenchmarks` | WorldV2 fixed-capacity command-stream set bursts over existing components to track transition-stable update throughput. |
| `EcsV2CommandStreamRemoveBurstBenchmarks` | WorldV2 fixed-capacity command-stream remove bursts over existing components to track structural transition throughput. |
| `EcsQueryCachePressureBenchmarks` | High-cardinality relationship-target queries to stress query-cache pressure scenarios. |
| `EcsV2HotPathBenchmarks` | WorldV2 compiled-query hot paths comparing cursor and direct struct-job (`Run`) loops on unmanaged components. |

## Run

```bash
dotnet run -c Release --project benchmarks/Bezoro.ECS.Benchmarks/Bezoro.ECS.Benchmarks.csproj
```

By default, this runs with BenchmarkDotNet `ShortRun` so local iterations finish quickly.

For lower-noise local comparisons, pass `--reliable` to use `MediumRun`:

```bash
dotnet run -c Release --project benchmarks/Bezoro.ECS.Benchmarks/Bezoro.ECS.Benchmarks.csproj -- --reliable
```

For exhaustive runs with BenchmarkDotNet default job settings, pass `--full`:

```bash
dotnet run -c Release --project benchmarks/Bezoro.ECS.Benchmarks/Bezoro.ECS.Benchmarks.csproj -- --full
```
