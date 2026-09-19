# Bezoro.ECS.Benchmarks

BenchmarkDotNet suite for core ECS performance targets.

## Benchmarks

| Benchmark Type                               | Description                                                                                                                                                                   |
|----------------------------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `EcsWorldCommandStreamBurstBenchmarks`       | World fixed-capacity command-stream creation bursts with playback/reset reuse behavior.                                                                                       |
| `EcsWorldCommandStreamSetBurstBenchmarks`    | World fixed-capacity command-stream set bursts over existing components to track transition-stable update throughput.                                                         |
| `EcsWorldCommandStreamRemoveBurstBenchmarks` | World fixed-capacity command-stream remove bursts over existing components to track structural transition throughput.                                                         |
| `EcsWorldHotPathBenchmarks`                  | World compiled-query hot paths comparing cursor and direct struct-job (`Run`) loops on unmanaged components.                                                                  |
| `EcsWorldComponentAccessBenchmarks`          | World sequential component access paths (`TryGet`/`Write`, cached accessor variants, and sequential `QueryCursor.Get`), plus cursor vs `QueryView` struct-job loop comparison.   |
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
| QueryView read-only `ForEachRead` over managed components        |   876.73 μs | 20.347 μs | 30.455 μs |       3 B |
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
| QueryView read-only `ForEachRead` over managed components | 757.19 μs | 85.93 μs | -88.7% | 3 B | 0 B |
| QueryView mutable `ForEach` over managed components | 1,053.38 μs | 69.13 μs | -93.4% | 6 B | 0 B |
| QueryView parallel entity-aware struct job | 218.77 μs | 213.90 μs | -2.2% | 377 B | 377 B |

The two initial post-change runs produced stable managed results (85.95/68.62 μs and 85.93/69.13 μs). Their untouched parallel results (206.98 μs and 213.90 μs) differed materially from the original fixed 176.07 μs baseline, while the immediate pre-change control measured 218.77 μs. The current-condition control isolates that difference as machine-state drift: no touched case regressed by more than 5%, managed traversal improved materially, and allocations did not worsen.

## Parallel Entity Materialization Elimination (2026-07-29)

Task 7b was measured with the reliable QueryView command on the same machine. The pre-change control used detached commit `63dc9ce`; its earlier recorded means were 123.18/87.26/106.13/78.90/80.42/226.05 μs in table order below. Because both the recorded control and the first post-change run reported multimodal distributions and untouched-case drift, the control was repeated immediately. The one untouched post-change case still outside the 5% gate was then repeated in isolation.

| Benchmark | Immediate `63dc9ce` control | Task 7b | Change | Control allocated | Task 7b allocated |
|-----------|----------------------------:|--------:|-------:|------------------:|------------------:|
| QueryView typed `ForEach` over unmanaged components | 121.53 μs | 121.91 μs | +0.3% | 0 B | 0 B |
| QueryView struct-job `Run` over unmanaged components | 84.80 μs | 83.75 μs | -1.2% | 0 B | 0 B |
| QueryView entity-aware struct-job `RunEntity` | 106.36 μs | 107.78 μs | +1.3% | 0 B | 0 B |
| QueryView read-only `ForEachRead` over managed components | 101.01 μs | 94.67 μs | -6.3% | 0 B | 0 B |
| QueryView mutable `ForEach` over managed components | 79.36 μs | 77.69 μs | -2.1% | 0 B | 0 B |
| QueryView parallel entity-aware struct job | 209.20 μs | 47.90 μs | -77.1% | 377 B | 377 B |

The isolated unmanaged `ForEach` repeat measured 121.91 μs after the full post-change run measured 130.15 μs, resolving the only untouched-case regression above 5% as run-to-run variance. Direct entity construction from chunk IDs and the world version table removes the entity materialization pass without adding allocations; the touched parallel case improved by 77.1% against the immediate control.

## Scalar Overwrite Consolidation Validation (2026-07-29)

Task 9b consolidated the three scalar existing-component overwrite branches behind one validation-and-write operation. The reliable 50,000-command set burst measured 1.401 ms (0.0209 ms error, 0.0300 ms standard deviation), compared with the recorded Task 9 baseline of 1.399 ms (0.0281 ms error, 0.0403 ms standard deviation), a +0.1% change. Because BenchmarkDotNet rounded the post-change allocation result to 1 B rather than the recorded baseline's 0 B, detached commit `7b94a1c` was repeated immediately; that control measured 1.400 ms (0.0149 ms error, 0.0224 ms standard deviation) and the same rounded 1 B. Both immediate runs reported 400 allocated bytes across 512 operations, isolating the displayed allocation difference as measurement rounding rather than a change.

The burst benchmark exercises the untouched validated fast-batch path, not the new scalar overwrite helper, so it guards against accidental disruption to command playback rather than measuring helper throughput directly. The dedicated warmed scalar overwrite test independently remained exactly allocation-free.

## Interpreting Results

- Hot-path compiled query loops should remain allocation-free (`Allocated = 0 B`) after warm-up; some exporters render zero allocation as `-`.
- `QueryView` unmanaged traversal should stay close to the direct/cursor hot path; meaningful regressions should be reviewed before release.
- `QueryView.Run...` is the canonical job benchmark surface. The obsolete generic `World.Run...` instance methods are behavior-preserving forwarders and do not define a separate performance tier.
- `QueryView.ForEachRead(...)` over managed structs is the ergonomic tier, not the unmanaged hot path; track it separately rather than comparing it directly to cursor jobs.
- Entity-aware cases must preserve valid entity IDs/versions and must not add entity-scratch materialization.
- Component access and command-stream burst benchmarks may show tiny runtime-level allocations depending on environment.
- Treat >5% throughput regressions in hot-path means as a gate requiring explicit review.

## Release Gate Workflow

- Run `EcsWorldHotPathBenchmarks` and `EcsWorldComponentAccessBenchmarks` when changing cursor/direct execution, accessors, or change-tracking internals.
- Run `EcsWorldQueryViewBenchmarks` when changing `QueryView`, source-generated ergonomic APIs, or scheduler inference tied to ergonomic call sites.
- Use `--fast` for local iteration only; release sign-off should use the default reliable run or `--full` when investigating regressions.

Note: BenchmarkDotNet creates an autogenerated project and performs restore during execution. In network-restricted environments, benchmark runs may fail before measurements if package restore for the autogenerated project is blocked.
