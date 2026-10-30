# Toolkit diagnostic measurements

Thirteen selected workloads were measured after setup with BenchmarkDotNet ShortRun: one launch, three warmups and three measured iterations. All reported 0 B of managed allocation per operation. Raw CSVs and unmodified BDN reports are adjacent; context JSON files pin the source revisions and filters.

The shared host was Debian 13 on an Intel Xeon Platinum 8573C with five visible cores, SDK 10.0.203 and .NET 10.0.7 x64. These .NET 10 timings do not measure Unity's .NET Standard implementation, Mono, IL2CPP, or a Godot export.

| Area | Workload | Parameters | Mean | Error | Allocated |
| --- | --- | --- | ---: | ---: | ---: |
| Inventory | TransferRoundTrip | Slots=16 | 55.14 ns | 20.26 ns | 0 B |
| Inventory | TransferRoundTrip | Slots=128 | 345.19 ns | 21.59 ns | 0 B |
| StateMachines | MatchedStep | TransitionCount=16 | 7.393 ns | 0.9807 ns | 0 B |
| StateMachines | MatchedStep | TransitionCount=1024 | 18.728 ns | 0.2568 ns | 0 B |
| StateMachines | MatchedStep | TransitionCount=65536 | 21.391 ns | 1.3802 ns | 0 B |
| Spatial | Circle | PointCount=1024 | 110.1 ns | 207.90 ns | 0 B |
| Spatial | Circle | PointCount=16384 | 1,288.4 ns | 72.19 ns | 0 B |
| Core | NegativeChunkCoordinate | fixed record/input | 0.2870 ns | 0.0498 ns | 0 B |
| Core | ExponentialDamping | fixed record/input | 8.0226 ns | 0.0735 ns | 0 B |
| Core | WriteGameplayRecord | fixed record/input | 2.749 ns | 0.0883 ns | 0 B |
| Core | ReadGameplayRecord | fixed record/input | 3.513 ns | 0.7338 ns | 0 B |
| Core | Count4096Flags | fixed record/input | 21.69 ns | 0.908 ns | 0 B |
| Core | LowerBound1024 | fixed record/input | 11.51 ns | 3.216 ns | 0 B |

Inventory round-trip means include two transfers of 37 items, with inventories restored to their starting totals. State lookup uses a fixed query in a warm sparse table. Spatial circle queries use deterministic uniform distributions and reusable buffers large enough for every point. Binary records have 41 bytes. Bit counting scans 4,096 flags; lower-bound lookup searches 1,024 integers. Damping rate, target and elapsed time come from runtime fields so the exponential is not folded to a constant.

Very small methods approach the measurement floor; the constant divisor in the chunk-coordinate case also permits strength reduction. Wide error intervals and fixed inputs limit comparisons. These diagnostics are not frame-budget guarantees. Profile the actual game, input distribution and target runtime. Construction, exception paths, file I/O and application serialization are outside the measured operations.

Reproduction filters are in each context JSON. Run the corresponding benchmark project in Release with those filters, `--job short --exporters csv`, and a fresh artifact directory.
