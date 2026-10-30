# Core benchmarks

BenchmarkDotNet workloads cover object pools, swap-back storage, deterministic random draws, weighted selection, shuffling, generational slot maps, and ring buffers.

```sh
dotnet run -c Release --project benchmarks/Bezoro.Core.Benchmarks -- --filter '*DeterministicRandomBenchmarks*' '*GameCollectionsBenchmarks*'
```

## Recorded foundation diagnostic

The [random CSV](results/2026-10-29-random-short-run.csv) and [collection CSV](results/2026-10-29-collections-short-run.csv) were collected on Debian 13, Intel Xeon Platinum 8573C, SDK 10.0.203 and .NET 10.0.7. Three warmups and three 100 ms measured iterations were used. Implementations correspond to `045cdad5` and `f169f62d`.

All ten workloads reported zero managed allocation per operation after setup. Weighted selection from 256 entries measured about 53.5 ns; shuffling 32 entries measured about 111.5 ns. Smaller operations approach the measurement floor and have wide relative error intervals. Inspect the raw data and method batching before comparing them. These short shared-host runs do not establish Unity/Godot timing guarantees.

## Recorded toolkit diagnostic

[Selected measurements and raw reports](../results/2026-10-30-toolkit/README.md) record the source revision, runtime, workload parameters and error intervals. Only the listed workloads were measured; the project also defines other profiling cases.
