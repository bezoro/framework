# Bezoro.ECS.Benchmarks

BenchmarkDotNet suite for core ECS performance targets.

## Benchmarks

| Benchmark Type                               | Description                                                                                                                                                                   |
|----------------------------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `EcsWorldCommandStreamBurstBenchmarks`       | World fixed-capacity command-stream creation bursts with playback/reset reuse behavior.                                                                                       |
| `EcsWorldCommandStreamSetBurstBenchmarks`    | World fixed-capacity command-stream set bursts over existing components to track transition-stable update throughput.                                                         |
| `EcsWorldCommandStreamRemoveBurstBenchmarks` | World fixed-capacity command-stream remove bursts over existing components to track structural transition throughput.                                                         |
| `EcsWorldHotPathBenchmarks`                  | World compiled-query hot paths comparing cursor and direct struct-job (`Run`) loops on unmanaged components.                                                                  |
| `EcsWorldComponentAccessBenchmarks`          | World sequential component access paths (`TryGet`/`Write`, cached accessor variants, and sequential `QueryCursor.Get`), plus cursor vs direct query struct-job loop comparison. |
| `EcsWorldQueryViewBenchmarks`                | Ergonomic `QueryView` paths covering managed read-only and mutable traversal, sequential and entity-aware struct jobs, and parallel entity-aware struct jobs.                  |

## Run

```bash
dotnet run -c Release --project benchmarks/Bezoro.ECS.Benchmarks/Bezoro.ECS.Benchmarks.csproj
```

By default, this runs with BenchmarkDotNet `MediumRun` (`--reliable` remains accepted as an alias):

```bash
dotnet run -c Release --project benchmarks/Bezoro.ECS.Benchmarks/Bezoro.ECS.Benchmarks.csproj -- --reliable
```

For quick local checks, pass `--fast` to use `ShortRun`:

```bash
dotnet run -c Release --project benchmarks/Bezoro.ECS.Benchmarks/Bezoro.ECS.Benchmarks.csproj -- --fast
```

For exhaustive runs with BenchmarkDotNet default job settings, pass `--full`:

```bash
dotnet run -c Release --project benchmarks/Bezoro.ECS.Benchmarks/Bezoro.ECS.Benchmarks.csproj -- --full
```

To focus on the ergonomic query tier only:

```bash
dotnet run -c Release --project benchmarks/Bezoro.ECS.Benchmarks/Bezoro.ECS.Benchmarks.csproj -- --fast --filter "*EcsWorldQueryViewBenchmarks*"
```

## Reliable Baseline (2026-07-28)

Captured with BenchmarkDotNet 0.14.0 on Windows 11, an AMD Ryzen 9 7900X, and .NET 9.0.18. Each case uses 100,000 entities and `ReliableRun` (2 launches, 10 warmups, and 15 measurement iterations).

| Benchmark                                                        | Mean        | Error     | StdDev    | Allocated |
|------------------------------------------------------------------|------------:|----------:|----------:|----------:|
| World compiled query sequential ref/in `ForEach`                 |    74.31 μs |  3.351 μs |  5.016 μs |       0 B |
| QueryView direct struct-job `Run`                                |    75.69 μs |  5.048 μs |  7.556 μs |       0 B |
| QueryView typed `ForEach` over unmanaged components              |   130.78 μs |  4.190 μs |  6.141 μs |       0 B |
| QueryView struct-job `Run` over unmanaged components             |    86.84 μs |  3.013 μs |  4.509 μs |       0 B |
| QueryView entity-aware struct-job `RunEntity`                    |   111.05 μs |  2.317 μs |  3.396 μs |       0 B |
| QueryView read-only `ForEach` over managed components            |   876.73 μs | 20.347 μs | 30.455 μs |       3 B |
| QueryView mutable `ForEach` over managed components              | 1,129.03 μs | 53.723 μs | 80.411 μs |       5 B |
| QueryView parallel entity-aware struct job                       |   232.72 μs | 19.609 μs | 29.351 μs |     377 B |

BenchmarkDotNet reported multimodal distributions for QueryView sequential `Run`, parallel entity-aware execution, and the world cursor path. Treat small changes within the reported error and variance cautiously; use the release gates below for comparisons.

## Direct Managed Traversal Validation (2026-07-28)

Task 5c was measured with the same reliable command and machine as the baseline. A detached `4d41a63` control run was repeated immediately under current machine conditions because the untouched parallel case had drifted from the original fixed baseline. The control artifact was generated at `BenchmarkDotNet.Artifacts/results/Bezoro.ECS.Benchmarks.EcsWorldQueryViewBenchmarks-report-github.md` inside the temporary validation worktree; that worktree was removed after the results were captured. The post-change artifact remains at the same relative path in this worktree.

| Benchmark | Current-condition control | Task 5c | Change | Control allocated | Task 5c allocated |
|-----------|--------------------------:|--------:|-------:|------------------:|------------------:|
| QueryView typed `ForEach` over unmanaged components | 105.99 μs | 106.83 μs | +0.8% | 0 B | 0 B |
| QueryView struct-job `Run` over unmanaged components | 73.45 μs | 71.23 μs | -3.0% | 0 B | 0 B |
| QueryView entity-aware struct-job `RunEntity` | 96.27 μs | 94.03 μs | -2.3% | 0 B | 0 B |
| QueryView read-only `ForEach` over managed components | 757.19 μs | 85.93 μs | -88.7% | 3 B | 0 B |
| QueryView mutable `ForEach` over managed components | 1,053.38 μs | 69.13 μs | -93.4% | 6 B | 0 B |
| QueryView parallel entity-aware struct job | 218.77 μs | 213.90 μs | -2.2% | 377 B | 377 B |

The two initial post-change runs produced stable managed results (85.95/68.62 μs and 85.93/69.13 μs). Their untouched parallel results (206.98 μs and 213.90 μs) differed materially from the original fixed 176.07 μs baseline, while the immediate pre-change control measured 218.77 μs. The current-condition control isolates that difference as machine-state drift: no touched case regressed by more than 5%, managed traversal improved materially, and allocations did not worsen.

## Interpreting Results

- Hot-path compiled query loops should remain allocation-free (`Allocated = -`) after warm-up.
- `QueryView` unmanaged traversal should stay close to the direct/cursor hot path; meaningful regressions should be reviewed before release.
- `QueryView.ForEachRead(...)` over managed structs is the ergonomic tier, not the unmanaged hot path; track it separately rather than comparing it directly to cursor jobs.
- Entity-aware cases must preserve valid entity IDs/versions and must not add entity-scratch materialization.
- Component access and command-stream burst benchmarks may show tiny runtime-level allocations depending on environment.
- Treat >5% throughput regressions in hot-path means as a gate requiring explicit review.

## Release Gate Workflow

- Run `EcsWorldHotPathBenchmarks` and `EcsWorldComponentAccessBenchmarks` when changing cursor/direct execution, accessors, or change-tracking internals.
- Run `EcsWorldQueryViewBenchmarks` when changing `QueryView`, source-generated ergonomic APIs, or scheduler inference tied to ergonomic call sites.
- Use `--fast` for local iteration only; release sign-off should use the default reliable run or `--full` when investigating regressions.

Note: BenchmarkDotNet creates an autogenerated project and performs restore during execution. In network-restricted environments, benchmark runs may fail before measurements if package restore for the autogenerated project is blocked.
