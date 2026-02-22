# Bezoro.ECS
High-performance fixed-capacity ECS runtime centered on `World`, `CommandStream`, compiled queries, and conflict-aware system scheduling.

## Types
| Type                                                                                                                       | Description                                                                                                                                  |
|----------------------------------------------------------------------------------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------|
| `World`                                                                                                                    | ECS runtime (`Services/World.cs`) for entity/component storage, command playback, system execution, snapshots, and compiled query execution. |
| `WorldConfig`                                                                                                              | Fixed-capacity runtime configuration (entity/component/query/command capacities, chunk capacity, parallelism, and overflow policy).          |
| `WorldOptions`                                                                                                             | Compatibility options surface mapped to `WorldConfig`.                                                                                       |
| `Entity`                                                                                                                   | Stable entity handle (`id`, `version`) with stale-handle invalidation semantics.                                                             |
| `CommandStream`                                                                                                            | Deferred structural mutation recorder (`CreateEntity`/`Set`/`Remove`/`Destroy`) with deterministic playback.                                 |
| `QueryBuilder` / `ICompiledQuerySpec` / `QueryHandle<TSpec>` / `QueryCursor`                                               | Compiled query pipeline with `All`/`Any`/`None`/`Optional`/`Changed`/`Added`/`Related` filters and hot-path iteration APIs.                  |
| `QueryDiagnostics`                                                                                                         | Query introspection snapshot (filters, cache state, matching archetype/chunk/entity counts).                                                 |
| `ISystem` / `SystemContext` / `SystemUpdateSettings` / `Stage` / `SystemLoopPhase`                                         | System contract and scheduling context for staged `Tick`/`FixedTick`/`LateTick` execution.                                                   |
| `ISystemRunCondition` / `SystemRunConditionContext`                                                                        | System/set run-condition contract for conditional scheduler execution.                                                                       |
| `ScheduleDiagnostics` / `SchedulePhaseDiagnostics` / `ScheduleStageDiagnostics` / `ScheduleBatchDiagnostics`               | Scheduler plan introspection snapshot for phases, stages, and execution batches.                                                             |
| `WorldDiagnostics` / `ArenaDiagnostics` / `CommandStreamDiagnostics`                                                       | Runtime diagnostics snapshots for capacity, overflow, and high-watermark visibility.                                                         |
| `WorldSnapshot` / `SnapshotEntityRecord` / `SnapshotComponentRecord` / `SnapshotRelationRecord` / `SnapshotResourceRecord` | Serializer-agnostic snapshot payload types for capture/restore workflows.                                                                    |

## Quick Start
```csharp
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;

var world = new World(new WorldConfig { EntityCapacity = 1024, QueryResultCapacity = 1024 });

using var commands = world.CreateCommandStream();
var entity = commands.CreateEntity();
commands.Set(entity, new Position { X = 1, Y = 2 });
world.Playback(commands);

var handle = world.Compile<PositionQuery>();
using var cursor = world.Execute(handle);
if (cursor.MoveNext())
{
    for (var i = 0; i < cursor.Current.Length; i++)
    {
        ref var position = ref cursor.Get<Position>(i);
        position.X += 10;
    }
}

readonly struct PositionQuery : ICompiledQuerySpec
{
    public void Build(ref QueryBuilder builder) => builder.All<Position>();
}

struct Position { public float X; public float Y; }
```

## API Reference
| Member                                                                                                                        | Description                                                                              |
|-------------------------------------------------------------------------------------------------------------------------------|------------------------------------------------------------------------------------------|
| `Spawn`, `Despawn`, `Add`, `Set`, `Remove`, `Get`, `TryGet`, `Has`, `IsAlive`                                                 | Core entity/component operations with versioned-handle safety.                           |
| `AddRelation<TRelation>`, `RemoveRelation<TRelation>`, `HasRelation<TRelation>`                                               | First-class relation API for source->target edges backed by relation marker components.  |
| `CreateCommandStream`, `Playback`                                                                                             | Deferred mutation recording and deterministic apply stage.                               |
| `Compile<TSpec>`, `Execute<TSpec>`, `ForEach(...)`, `Run(...)`, `RunParallel(...)`                                            | Compiled query plan creation plus sequential/parallel hot-path iteration helpers.        |
| `GetQueryDiagnostics<TSpec>`                                                                                                  | Snapshot of compiled query filters, cache status, and current match counts.              |
| `AddSystem`, `Tick`, `FixedTick`, `LateTick`, `RunPhase`                                                                      | System registration and phase-based scheduling.                                          |
| `SetSystemSetEnabled<TSet>`, `IsSystemSetEnabled<TSet>`, `SetSystemSetRunCondition<TSet>`, `ClearSystemSetRunCondition<TSet>` | Runtime system-set controls and set-level run conditions.                                |
| `GetResource<T>`, `SetResource<T>`                                                                                            | Type-indexed resource storage.                                                           |
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
- Compiled queries are defined via `ICompiledQuerySpec.Build(ref QueryBuilder builder)`.
- Supported runtime filters: `All<T>`, `Any<T>`, `None<T>`, `Optional<T>`, `Changed<T>`, `Added<T>`, `Related<TRelation>(target)` / `Related<TRelation>()`.
- `Changed<T>` / `Added<T>` are evaluated incrementally per compiled handle execution.
- Change tracking also covers mutable ref-based write surfaces (`World.Get`, component accessors, cursor/world `ForEach`, cursor/world `Run`, and `RunParallel`) once any changed/added query is compiled.
- `GetQueryDiagnostics(handle)` reports static filter makeup and current dynamic match counts without advancing incremental `Changed`/`Added` windows.
- Execution styles:
- Cursor style: `using var cursor = world.Execute(handle);`
- Direct style: `world.ForEach(handle, ...)` / `world.Run(handle, job)`
- Parallel direct style: `world.RunParallel(handle, job, degreeOfParallelism: 4)`

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
        AllowedReferenceResourceTypes = [typeof(GameConfigResource)]
    }
);
```

### Snapshot Notes
- Restore clears world state and resources before replaying snapshot payload.
- Snapshot relation records replay through relation APIs with deterministic source-target mapping.
- Include explicit allow-lists for reference resources when loading untrusted input.
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
- [x] Generated job extensions for cursor/world `Run(job)` ergonomics
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
