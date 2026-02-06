# Bezoro.ECS.Benchmarks

BenchmarkDotNet suite for core ECS performance targets.

## Benchmarks

| Benchmark Type | Description |
|---|---|
| `EcsHotPathBenchmarks` | Iteration throughput, parallel chunk iteration, entity lookup, and query cache-hit path. |
| `EcsStructuralBenchmarks` | Entity create/destroy throughput and structural add-component costs. |

## Run

```bash
dotnet run -c Release --project benchmarks/Bezoro.ECS.Benchmarks/Bezoro.ECS.Benchmarks.csproj
```
