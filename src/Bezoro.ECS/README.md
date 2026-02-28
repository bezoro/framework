# Bezoro.ECS
High-performance fixed-capacity ECS runtime centered on `World`, `CommandBuffer`, `QueryView`, compiled queries, and conflict-aware system scheduling.

## Types
| Type                                                                                                                       | Description                                                                                                                                  |
|----------------------------------------------------------------------------------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------|
| `World`                                                                                                                    | ECS runtime (`Services/World.cs`) for entity/component storage, command playback, system execution, snapshots, and compiled query execution. |
| `WorldConfig`                                                                                                              | Fixed-capacity runtime configuration (entity/component/query/command capacities, chunk capacity, parallelism, and overflow policy).          |
| `WorldOptions`                                                                                                             | Compatibility options surface mapped to `WorldConfig`.                                                                                       |
| `Entity`                                                                                                                   | Stable entity handle (`id`, `version`) with stale-handle invalidation semantics.                                                             |
| `CommandBuffer` / `CommandStream`                                                                                          | Deferred structural mutation recorder. `CommandBuffer` is the ergonomic surface; `CommandStream` remains the lower-level implementation.     |
| `QueryView<TSpec>` / `QueryBuilder` / `ICompiledQuerySpec` / `QueryHandle<TSpec>` / `QueryCursor`                          | Query authoring and execution surfaces. `QueryView<TSpec>` is the ergonomic path; handles/cursors remain the low-level hot-path APIs.        |
| `QueryDiagnostics`                                                                                                         | Query introspection snapshot (filters, cache state, matching archetype/chunk/entity counts).                                                 |
| `ISystem` / `SystemContext` / `SystemUpdateSettings` / `Stage` / `SystemLoopPhase`                                         | System contract and scheduling context for staged `Tick`/`FixedTick`/`LateTick` execution.                                                   |
| `ISystemRunCondition` / `SystemRunConditionContext`                                                                        | System/set run-condition contract for conditional scheduler execution.                                                                       |
| `ScheduleDiagnostics` / `SchedulePhaseDiagnostics` / `ScheduleStageDiagnostics` / `ScheduleBatchDiagnostics`               | Scheduler plan introspection snapshot for phases, stages, and execution batches.                                                             |
| `WorldDiagnostics` / `ArenaDiagnostics` / `CommandStreamDiagnostics`                                                       | Runtime diagnostics snapshots for capacity, overflow, and high-watermark visibility.                                                         |
| `WorldSnapshot` / `SnapshotEntityRecord` / `SnapshotComponentRecord` / `SnapshotRelationRecord` / `SnapshotResourceRecord` | Serializer-agnostic snapshot payload types for capture/restore workflows.                                                                    |

## Quick Start
```csharp
using Bezoro.ECS.Attributes;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;

var world = new World(new WorldConfig { EntityCapacity = 1024, QueryResultCapacity = 1024 });

var entity = world.Spawn(new Position { X = 1, Y = 2 });

world.Query<PositionQuery>().ForEach(
    (entityId, ref Position position) =>
    {
        position.X += 10;
    });

[Query]
[With<Position>]
readonly partial struct PositionQuery;

struct Position { public float X; public float Y; }
```
Within this solution, application code should reference `Bezoro.ECS`; the source generator project is wired in as analyzer infrastructure and is not intended as a separate application-facing dependency.

## API Reference
| Member                                                                                                                        | Description                                                                              |
|-------------------------------------------------------------------------------------------------------------------------------|------------------------------------------------------------------------------------------|
| `Spawn`, `Despawn`, `Add`, `Replace`, `Remove`, `Read`, `Write`, `TryRead`, `TryWrite`, `Has`, `IsAlive`                      | Core entity/component operations with explicit read/write intent.                        |
| `AddRelation<TRelation>`, `RemoveRelation<TRelation>`, `HasRelation<TRelation>`                                               | First-class relation API for source->target edges backed by relation marker components.  |
| `CreateCommandBuffer`, `CreateCommandStream`, `Playback`                                                                      | Deferred mutation recording and deterministic apply stage.                               |
| `Query<TSpec>`, `Compile<TSpec>`, `Execute<TSpec>`, `ForEach(...)`, `Run(...)`, `RunParallel(...)`, `RunEntity(...)`          | Ergonomic and low-level query execution surfaces.                                        |
| `GetQueryDiagnostics<TSpec>`                                                                                                  | Snapshot of compiled query filters, cache status, and current match counts.              |
| `AddSystem`, `Tick`, `FixedTick`, `LateTick`, `RunPhase`                                                                      | System registration and phase-based scheduling.                                          |
| `SetSystemSetEnabled<TSet>`, `IsSystemSetEnabled<TSet>`, `SetSystemSetRunCondition<TSet>`, `ClearSystemSetRunCondition<TSet>` | Runtime system-set controls and set-level run conditions.                                |
| `HasResource<T>`, `ReadResource<T>`, `WriteResource<T>`, `TryReadResource<T>`, `GetOrCreateResource<T>`, `ReplaceResource<T>` | Type-indexed resource storage with explicit read/write/create semantics.                 |
| `CaptureSnapshot<TWriter>`, `RestoreSnapshot<TReader>`                                                                        | Snapshot capture/restore with serializer-owned transport via reader/writer abstractions. |
| `GetScheduleDiagnostics`                                                                                                      | Snapshot of current scheduler phase/stage/batch plan and registered system count.        |
| `GetDiagnostics`, `CommandStream.GetDiagnostics`                                                                              | Capacity/overflow/high-watermark diagnostics.                                            |

## Scheduling Model
- Systems run by `SystemLoopPhase` (`Tick`, `FixedTick`, `LateTick`) and ordered `Stage` (`Input`, `PreTick`, `Tick`, `PostTick`, `Render`).
- The scheduler batches systems for parallel execution using read/write metadata (`[Reads<T>]`, `[Writes<T>]`, `[ReadsResource<T>]`, `[WritesResource<T>]`, `[Exclusive]`).
- Explicit ordering constraints are supported with `[Before<TSystem>]` and `[After<TSystem>]` within the same phase+stage plan.
- Systems can be grouped into sets via `[SystemSet<TSet>]` and toggled at runtime with `SetSystemSetEnabled<TSet>(...)`.
- Conditional execution is supported via `[RunIf<TRunCondition>]` (per-system) and `SetSystemSetRunCondition<TSet>(...)` (per-set), both implementing `ISystemRunCondition`.
- Structural writes are recorded per-system in command streams and flushed deterministically after each batch.
- Fixed-interval scheduling is supported via `SystemUpdateSettings.FixedInterval(...)` with bounded catch-up.

## Query Model
- Ergonomic queries are typically declared with `[Query]` plus `[With]` / `[Without]` / `[AnyOf]` attributes and consumed through `World.Query<TSpec>()`.
- `ICompiledQuerySpec.Build(ref QueryBuilder builder)` remains available for advanced/manual query authoring.
- Runtime-parameterized query instances are not part of the public 1.0 surface yet; ergonomic queries are compiled by specification type.
- Supported runtime filters: `With<T>` / `All<T>`, `AnyOf<T>` / `Any<T>`, `Without<T>` / `None<T>`, `Optional<T>`, `Changed<T>`, `Added<T>`, `Related<TRelation>(target)` / `Related<TRelation>()`.
- `Changed<T>` / `Added<T>` are evaluated incrementally per compiled handle execution.
- Change tracking also covers mutable ref-based write surfaces (`World.Get`, component accessors, cursor/world `ForEach`, cursor/world `Run`, and `RunParallel`) once any changed/added query is compiled.
- `GetQueryDiagnostics(handle)` reports static filter makeup and current dynamic match counts without advancing incremental `Changed`/`Added` windows.
- Execution styles:
- QueryView style: `world.Query<MyQuery>().ForEach((entity, ref Position position) => { ... })`
- QueryView read-only style: `world.Query<MyQuery>().ForEachRead((entity, in ActivationEntry entry) => { ... })`
- QueryView job style: `world.Query<MyQuery>().Run(new IntegrateJob(dt))`
- QueryView entity-aware job style: `world.Query<MyQuery>().Run(new IntegrateEntityJob(dt))`
- Cursor style: `using var cursor = world.Execute(handle);`
- Cursor entity-aware job style: `cursor.Run(new IntegrateEntityJob(dt))`
- Direct style: `world.ForEach(handle, ...)` / `world.Run(handle, job)`
- Direct entity-aware job style: `world.Run(handle, new IntegrateEntityJob(dt))`
- Parallel direct style: `world.RunParallel(handle, job, degreeOfParallelism: 4)`
- Parallel QueryView entity-aware job style: `world.Query<MyQuery>().RunParallel(new IntegrateEntityJob(dt), degreeOfParallelism: 4)`
- The public instance methods carrying entity-aware jobs are named `RunEntity(...)` / `RunParallelEntity(...)`; the source generator emits `Run(...)` / `RunParallel(...)` extensions for `IForEachEntity<T...>` jobs so gameplay code stays symmetrical with the non-entity-aware path.
- Typed `QueryView.ForEach(...)` now works for any struct component, including structs that contain references.
- `QueryView` job execution and the lower-level cursor/direct hot paths remain unmanaged-only and are still the performance-oriented escape hatch.

## Snapshot Serialization
`Bezoro.ECS` keeps snapshot transport explicit and engine-agnostic.
The runtime provides capture/restore payload primitives (`WorldSnapshot`) and reader/writer hooks, while applications own serialization format and storage.

### Recommended Pattern
1. Capture snapshot payload from world.
2. Serialize `WorldSnapshot` with your serializer of choice (`System.Text.Json`, MessagePack, etc.).
3. Deserialize back into `WorldSnapshot`.
4. Restore into a world with explicit deserialization options.

### Capture Example
```csharp
using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Types;

public struct SnapshotWriter : IWorldSnapshotWriter
{
    public WorldSnapshot Snapshot { get; private set; }
    public void Write(in WorldSnapshot snapshot) => Snapshot = snapshot;
}

var writer = new SnapshotWriter();
world.CaptureSnapshot(ref writer);
var snapshot = writer.Snapshot;
```

### Restore Example
```csharp
using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Options;
using Bezoro.ECS.Types;

public readonly struct SnapshotReader(WorldSnapshot snapshot) : IWorldSnapshotReader
{
    public WorldSnapshot Read() => snapshot;
}

var reader = new SnapshotReader(snapshot);
world.RestoreSnapshot(
    ref reader,
    new SnapshotDeserializationOptions
    {
        AllowedComponentTypes = [typeof(Position), typeof(Velocity)],
        AllowedRelationTypes = [typeof(Follows)],
        AllowedResourceTypes = [typeof(GameConfigResource)]
    }
);
```

### Snapshot Notes
- Restore validates the entire payload before mutating world state.
- Restore defaults to deny-by-default for snapshot component, relation, and resource types.
- Use explicit allow-lists for untrusted or cross-boundary snapshot input.
- `AllowAllComponentTypes` / `AllowAllRelationTypes` / `AllowAllResourceTypes` are intended for trusted same-process restore flows only.
- Snapshot relation records replay through relation APIs with deterministic source-target mapping.
- `IWorldSnapshotReader` still owns serializer behavior and type materialization; `RestoreSnapshot` validates the already-materialized `WorldSnapshot`.
- Keep snapshot DTO versions stable so old saves can be migrated safely.

## Query Diagnostics
`World.GetQueryDiagnostics(handle)` provides non-mutating insight into compiled query plans.

### What It Reports
- `MatchingArchetypeCount`: Number of archetypes matching static filters.
- `MatchingChunkCount`: Number of contiguous chunk ranges matching current static + dynamic filters.
- `MatchingEntityCount`: Number of entities currently matched.
- `ArchetypeCacheVersion` and `IsArchetypeCacheUpToDate`: Archetype-cache freshness state.
- Filter surfaces: `AllTypes`, `AnyTypes`, `NoneTypes`, `OptionalTypes`, `AddedTypes`, `ChangedTypes`, `RelatedRelationType`, `RelatedTarget`.

### Behavior Contract
- Calling diagnostics does not advance incremental `Changed<T>`/`Added<T>` windows.
- Diagnostics validates query-handle ownership exactly like direct query iteration APIs.
- Diagnostics cannot run while an active `QueryCursor` exists.

### Query Diagnostics Example
```csharp
var handle = world.Compile<MovingQuery>();
var diagnostics = world.GetQueryDiagnostics(handle);

Console.WriteLine($"Entities={diagnostics.MatchingEntityCount}");
Console.WriteLine($"All={string.Join(", ", diagnostics.AllTypes.Select(t => t.Name))}");
```

### Query Diagnostics Notes
- Diagnostics reuses the same internal matching pipeline as execution paths.
- Treat diagnostics as an introspection aid, not as a gameplay hot path.

## Performance And Allocation Contract
This section defines allocation and throughput expectations for `Bezoro.ECS` APIs.

### Core Guarantees
- Compiled-query direct/cursor loops are designed for zero-allocation steady-state usage after warm-up.
- `RunParallel` uses chunk-partitioned workers and preserves the same component-access model as sequential `Run`.
- Structural changes remain explicit through `CommandStream` and deterministic playback boundaries.

### Practical Allocation Expectations
- Hot-path query loops (`Run`, `ForEach`, cursor loops): target `0 B` steady-state.
- Component `Get`/`TryGet` surfaces: optimized for throughput; tiny runtime-level allocations may still appear in certain benchmark environments.
- Command-stream burst scenarios: optimized for fixed-capacity reuse and deterministic playback; tiny runtime-level allocations can still surface in measurement noise.

### Threading And Mutation Rules
- `World` direct API access is not thread-safe.
- `RunParallel` parallelizes component iteration only; structural world mutations still require command recording/playback.
- Query cursor and direct/diagnostics APIs are mutually exclusive while a cursor is active.

## Support Boundaries
- The ergonomic public query surface is `World.Query<TSpec>()` plus `QueryView<TSpec>`; runtime query instances are intentionally not part of the public contract yet.
- `QueryView.ForEach...` supports any `struct` component, including structs with managed references.
- Job-based query execution (`Run(...)`, `RunParallel(...)`, cursor/direct hot paths) remains the unmanaged-only performance tier.
- `SystemContext.Commands` and `CommandBuffer` are the primary deferred-mutation surface for systems; `CommandStream` remains available as the lower-level implementation API.
- Direct `World` access is single-threaded by contract; parallelism is coordinated through scheduler batches and `RunParallel`.

### Changed/Added Tracking Cost Model
- `Changed<T>` and `Added<T>` tracking activates when at least one compiled query uses these filters.
- Once active, mutable ref-based write surfaces (`Get`, accessors, cursor/direct runs, `RunParallel`) mark change metadata.
- This enables incremental query semantics but adds bookkeeping work proportional to touched entities/chunks.

### Benchmark References
- `../../BenchmarkDotNet.Artifacts/results/Bezoro.ECS.Benchmarks.EcsWorldHotPathBenchmarks-report-github.md`
- `../../BenchmarkDotNet.Artifacts/results/Bezoro.ECS.Benchmarks.EcsWorldComponentAccessBenchmarks-report-github.md`
- `../../BenchmarkDotNet.Artifacts/results/Bezoro.ECS.Benchmarks.EcsWorldCommandStreamBurstBenchmarks-report-github.md`
- `../../BenchmarkDotNet.Artifacts/results/Bezoro.ECS.Benchmarks.EcsWorldCommandStreamSetBurstBenchmarks-report-github.md`
- `../../BenchmarkDotNet.Artifacts/results/Bezoro.ECS.Benchmarks.EcsWorldCommandStreamRemoveBurstBenchmarks-report-github.md`

## Gold-Standard Checklist
Canonical parity tracker against the Bevy + Unity ECS reference bar.

- Last updated: 2026-02-22
- Status legend:
- `[ ]` Not started
- `[~]` In progress / partial
- `[x]` Implemented and validated

### Core Runtime
- [x] Archetype + chunk storage model
- [x] Versioned entity handles (`id` + `version`)
- [x] Deferred structural mutation via `CommandStream` + explicit playback
- [x] Fixed-capacity world with overflow policy (`FailFast`/`DropNewest`)
- [x] World/resource APIs for game-loop usage
- [x] Public first-class relation API (`AddRelation`/`RemoveRelation`/`HasRelation`)
- [x] Relation-aware public query filters

### Query System
- [x] Compiled query specs (`ICompiledQuerySpec`)
- [x] Query DSL support for `All`/`Any`/`None`
- [x] Cursor-based and direct hot-path iteration
- [x] Struct job execution (`Run<TJob,...>`) on cursor and world
- [x] Entity-aware struct job execution (`IForEachEntity<T...>`) on `QueryView`, cursor, and world
- [x] Source-generated query spec implementation from `[Query]` + `[All]`/`[Any]`/`[None]`/`[Optional]`/`[Changed]`/`[Added]`
- [x] Optional component filter support (`Optional<T>`) in runtime query execution
- [x] Changed/added component filters (`Changed<T>`/`Added<T>`) in runtime query execution

### Scheduling And Safety
- [x] Stage + loop phase execution model
- [x] Access-based parallel batching using read/write metadata
- [x] Exclusive-system semantics
- [x] Fixed-interval/catch-up update settings
- [x] User-defined schedule graph dependencies (`Before`/`After`)
- [x] System sets and run-conditions API surface
- [x] Resource access participation in conflict graph

### Source Generation
- [x] System metadata catalog generation
- [x] Metadata inference from modern ECS iteration APIs (`World.ForEach/Run`, `QueryCursor.ForEach/Run`)
- [x] Generated job extensions for `QueryView` / cursor / world `Run(job)` ergonomics
- [x] Generated entity-aware job extensions for `IForEachEntity<T...>`
- [x] Query generator no longer emits stale `Query`/`IQuery` runtime references
- [x] Split-field generator support
- [x] Source-generated diagnostics for unsupported advanced query filters

### Diagnostics And Tooling
- [x] World/command diagnostics types
- [x] Benchmark project for ECS hot paths
- [x] Schedule diagnostics and graph introspection APIs
- [x] Public snapshot serialization/deserialization docs and examples

### Documentation
- [x] `src/Bezoro.ECS/README.md` accurately reflects current ECS capabilities
- [x] `src/Bezoro.ECS.SourceGen/README.md` aligned with current generators
- [x] `benchmarks/Bezoro.ECS.Benchmarks/README.md` aligned with runnable benchmark reality
- [x] `tests/Bezoro.ECS.Tests/README.md` exists

### Verification Gates
- [x] `dotnet build bezoro.framework.sln`
- [x] `dotnet test` verification across non-UCI test projects after the full ECS change set (UCI intentionally skipped due known flakiness)
- [x] Benchmark baseline + post-change comparison in this environment

### Notes
- Network-restricted environments can block BenchmarkDotNet auto-generated project restore. Keep benchmark regressions as a required gate in environments with NuGet connectivity.
- Latest fast-run baseline/post samples (2026-02-22): `EcsWorldHotPathBenchmarks` stayed in the ~58-60 us range with zero allocations; treat variance from `--fast` as expected and re-run with reliable jobs for tighter confidence.

## Design Notes
- Default runtime is `src/Bezoro.ECS/Services/World.cs`.
- The hot path is data-oriented with fixed-capacity buffers and explicit command playback to minimize allocations.
- Compiled queries are intended for sequential, cache-friendly component access in gameplay loops.
- `World` is not thread-safe for concurrent direct API access; scheduler and `RunParallel` parallelism are coordinated internally.
- Source generators in `Bezoro.ECS.SourceGen` can generate query spec and job helper code aligned with the compiled-query runtime.
