# Bezoro.Simulation.Benchmarks

BenchmarkDotNet workloads for the simulation module. Each benchmark keeps setup outside the measured operation and reports managed allocations.

```sh
dotnet run -c Release --project benchmarks/Bezoro.Simulation.Benchmarks -- --filter '*'
```

Use representative capacities and workloads when interpreting results. Small operations can be dominated by dispatch or measurement overhead. Results on desktop .NET do not establish Unity Mono, IL2CPP, or Godot frame budgets.

## Recorded attribute diagnostic

The [attribute CSV](results/2026-10-29-attributes-short-run.csv) contains a local ShortRun on Debian 13, Intel Xeon Platinum 8573C, SDK 10.0.203 and .NET 10.0.7. Three warmups and three 100 ms measured iterations were used. The measured implementation corresponds to `d1a9a23a`.

Cached reads measured 2.55–2.68 ns across 0, 8, and 64 modifiers. Changing the base value and evaluating 64 modifiers measured 222.7 ns; the pure span calculation measured 196.1 ns. All nine workloads reported zero managed bytes per operation. These are short diagnostics on shared hardware, with wide error intervals. Sub-nanosecond differences and ratios should not drive engine frame-budget decisions.

## Recorded timing diagnostic

The [timing CSV](results/2026-10-29-timing-short-run.csv) uses the same host/runtime and three warmups plus three 100 ms measured iterations. All six workloads reported zero managed bytes per operation after setup. The measured runtime implementation corresponds to `907bdb02`.

Clock advancement measured about 7.2–7.6 ns. Scheduling and draining a batch of 1,024 items measured about 59.0 µs; scheduling and canceling that batch in reverse insertion order measured about 9.75 µs. Scheduler timings are per complete batch, not per item, and the cancellation order is a favorable heap case. Profile the order and due-time distribution used by the game.
