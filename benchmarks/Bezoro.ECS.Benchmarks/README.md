# Bezoro.ECS.Benchmarks

BenchmarkDotNet suite for core ECS performance targets.

## Benchmarks

| Benchmark Type | Description |
|---|---|
| `EcsHotPathBenchmarks` | Iteration throughput, parallel chunk iteration, entity lookup, query cache-hit path, and wildcard relationship queries. |
| `EcsCommandBufferBenchmarks` | Command recording/playback throughput for deferred component updates. |
| `EcsStructuralBenchmarks` | Entity create/destroy throughput and structural add-component costs. |

## Run

```bash
dotnet run -c Release --project benchmarks/Bezoro.ECS.Benchmarks/Bezoro.ECS.Benchmarks.csproj
```
