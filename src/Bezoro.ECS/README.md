# Bezoro.ECS
High-performance fixed-capacity ECS runtime centered on `World`, `CommandStream`, compiled queries, and conflict-aware system scheduling.

## Types
| Type                                                                                                         | Description                                                                                                                         |
|--------------------------------------------------------------------------------------------------------------|-------------------------------------------------------------------------------------------------------------------------------------|
| `World`                                                                                                      | ECS runtime (`Services/World.cs`) for entity/component storage, command playback, system execution, and compiled query execution.   |
| `WorldConfig`                                                                                                | Fixed-capacity runtime configuration (entity/component/query/command capacities, chunk capacity, parallelism, and overflow policy). |
| `WorldOptions`                                                                                               | Compatibility options surface mapped to `WorldConfig`.                                                                              |
| `Entity`                                                                                                     | Stable entity handle (`id`, `version`) with stale-handle invalidation semantics.                                                    |
| `CommandStream`                                                                                              | Deferred structural mutation recorder (`CreateEntity`/`Set`/`Remove`/`Destroy`) with deterministic playback.                        |
| `QueryBuilder` / `ICompiledQuerySpec` / `QueryHandle<TSpec>` / `QueryCursor`                                 | Compiled query pipeline with `All`/`Any`/`None`/`Optional`/`Changed`/`Added`/`Related` filters and hot-path iteration APIs.         |
| `ISystem` / `SystemContext` / `SystemUpdateSettings` / `Stage` / `SystemLoopPhase`                           | System contract and scheduling context for staged `Tick`/`FixedTick`/`LateTick` execution.                                          |
| `ISystemRunCondition` / `SystemRunConditionContext`                                                          | System/set run-condition contract for conditional scheduler execution.                                                              |
| `ScheduleDiagnostics` / `SchedulePhaseDiagnostics` / `ScheduleStageDiagnostics` / `ScheduleBatchDiagnostics` | Scheduler plan introspection snapshot for phases, stages, and execution batches.                                                    |
| `WorldDiagnostics` / `ArenaDiagnostics` / `CommandStreamDiagnostics`                                         | Runtime diagnostics snapshots for capacity, overflow, and high-watermark visibility.                                                |

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
| Member                                                                                                                        | Description                                                                                     |
|-------------------------------------------------------------------------------------------------------------------------------|-------------------------------------------------------------------------------------------------|
| `Spawn`, `Despawn`, `Add`, `Set`, `Remove`, `Get`, `TryGet`, `Has`, `IsAlive`                                                 | Core entity/component operations with versioned-handle safety.                                  |
| `AddRelation<TRelation>`, `RemoveRelation<TRelation>`, `HasRelation<TRelation>`                                               | First-class relation API for source->target edges backed by relation marker components.         |
| `CreateCommandStream`, `Playback`                                                                                             | Deferred mutation recording and deterministic apply stage.                                      |
| `Compile<TSpec>`, `Execute<TSpec>`, `ForEach(...)`, `Run(...)`                                                                | Compiled query plan creation and hot-path iteration helpers for delegate and struct-job styles. |
| `AddSystem`, `Tick`, `FixedTick`, `LateTick`, `RunPhase`                                                                      | System registration and phase-based scheduling.                                                 |
| `SetSystemSetEnabled<TSet>`, `IsSystemSetEnabled<TSet>`, `SetSystemSetRunCondition<TSet>`, `ClearSystemSetRunCondition<TSet>` | Runtime system-set controls and set-level run conditions.                                       |
| `GetResource<T>`, `SetResource<T>`                                                                                            | Type-indexed resource storage.                                                                  |
| `GetScheduleDiagnostics`                                                                                                      | Snapshot of current scheduler phase/stage/batch plan and registered system count.               |
| `GetDiagnostics`, `CommandStream.GetDiagnostics`                                                                              | Capacity/overflow/high-watermark diagnostics.                                                   |

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
- Change tracking also covers mutable ref-based write surfaces (`World.Get`, component accessors, cursor/world `ForEach`, cursor/world `Run`) once any changed/added query is compiled.
- Execution styles:
- Cursor style: `using var cursor = world.Execute(handle);`
- Direct style: `world.ForEach(handle, ...)` / `world.Run(handle, job)`
- Cursor and direct styles are designed for no-allocation hot paths after warm-up.

## Snapshot Docs
- Snapshot serialization/deserialization guidance and examples are documented in `docs/ecs-snapshot-serialization.md`.

## Design Notes
- Default runtime is `src/Bezoro.ECS/Services/World.cs`.
- The hot path is data-oriented with fixed-capacity buffers and explicit command playback to minimize allocations.
- Compiled queries are intended for sequential, cache-friendly component access in gameplay loops.
- `World` is not thread-safe for concurrent direct API access; scheduler parallelism is coordinated internally by execution batches.
- Source generators in `Bezoro.ECS.SourceGen` can generate query spec and job helper code aligned with the compiled-query runtime.
