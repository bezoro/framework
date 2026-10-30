# Bezoro.Navigation.Benchmarks

BenchmarkDotNet workloads for the navigation module. Each benchmark keeps setup outside the measured operation and reports managed allocations.

```sh
dotnet run -c Release --project benchmarks/Bezoro.Navigation.Benchmarks -- --filter '*'
```

Use representative capacities and workloads when interpreting results. Small operations can be dominated by dispatch or measurement overhead. Results on desktop .NET do not establish Unity Mono, IL2CPP, or Godot frame budgets.

## Recorded pathfinding diagnostic

The [pathfinding CSV](results/2026-10-29-grid-pathfinding-short-run.csv) contains a local ShortRun on Debian 13, Intel Xeon Platinum 8573C, SDK 10.0.203 and .NET 10.0.7. Three warmups and three measured iterations were used. The measured runtime implementation corresponds to `72c40e6d`; the later 64-bit regression changed tests only.

| Map | 64 × 64 | 256 × 256 |
| --- | ---: | ---: |
| Open | 7.155 µs | 34.713 µs |
| Weighted | 480.639 µs | 9.663 ms |
| Maze | 263.561 µs | 6.127 ms |

All six workloads reported zero managed allocation per query after setup. These are complete searches on the checked-in benchmark maps. They do not bound search time on other maps. Weight distribution and obstacle layout change the amount of work substantially; use expansion budgets and independently owned workspaces when scheduling queries.
