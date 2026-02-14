# Bezoro.ECS

Bezoro.ECS is an archetype-based Entity Component System focused on staged system execution, chunk queries, and deferred structural changes.

## Types

| Type                                                            | Description                                                                                          |
|-----------------------------------------------------------------|------------------------------------------------------------------------------------------------------|
| `World`                                                         | ECS root for entities, components, resources, systems, queries, and snapshots.                       |
| `WorldV2`                                                       | Fixed-capacity ECS runtime with compiled queries and deferred structural command streams.             |
| `Entity`                                                        | 8-byte versioned handle (`Id`, `Version`) with `Entity.None` and `Entity.Wildcard`.                  |
| `Archetype`                                                     | Exact component-set storage identity backed by chunked columns.                                      |
| `Query`                                                         | Cached archetype query with `All/None/Any/Optional/Changed/Related` filters.                         |
| `IQuery`                                                        | Source-generated query definition contract for `world.Query<TQuery>()` entrypoints.                  |
| `ChunkView`                                                     | Span-based access to entities and component columns in a chunk.                                      |
| `CommandBuffer`                                                 | Deferred structural changes (`Create/Destroy/Add/Set/Remove`) for safe playback.                     |
| `CommandStream`                                                 | V2 fixed-capacity deferred structural stream (`Create/Destroy/Set`) with reusable buffers.           |
| `ICompiledQuerySpec` / `QueryHandle<TSpec>` / `QueryCursor`    | V2 compiled-query contracts and execution primitives.                                                 |
| `IForEach<T...>`                                                | Job-style query executor contracts for arity 1-4 (`ref` first component, `in` remaining components). |
| `[SplitFields]` / `[SplitGroup]`                                | Opt-in split storage annotations consumed by source-generated split helpers.                         |
| `OnAddObserver<T>` / `OnSetObserver<T>` / `OnRemoveObserver<T>` | Typed observer delegates for add/set/remove hooks with `ref`/`in` semantics.                         |
| `WorldDiagnostics` / `ArchetypeDiagnostics`                     | Snapshot diagnostics for archetype/chunk/entity memory usage.                                        |
| `SystemContext`                                                 | Per-system execution context (`DeltaTime`, `Stage`, `Commands`).                                     |
| `SystemLoopPhase`                                               | Host loop routing: `Tick`, `FixedTick`, `LateTick`.                                                  |
| `Stage`                                                         | System stage ordering: `Input`, `PreTick`, `Tick`, `PostTick`, `Render`.                             |
| `SnapshotDeserializationOptions`                                | Snapshot type-resolution and reference-resource allowlist options for secure deserialization.        |
| `ISystem`                                                       | System contract with optional lifecycle hooks and stage metadata.                                    |
| `IWorld`                                                        | Restricted world contract for systems.                                                               |

## Quick Start

```csharp
using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;

var world = new World("Main");

public struct Position : IComponent { public float X; public float Y; }
public struct Velocity : IComponent { public float X; public float Y; }
public readonly struct ChildOf;

var parent = world.Spawn();
var child = world.Spawn();

world.Add(child, new Position { X = 1, Y = 2 });
world.Add(child, new Velocity { X = 0.5f, Y = 0.25f });
world.Add<ChildOf>(child, parent);

world.Query()
    .All<Position>()
    .All<Velocity>()
    .Related<ChildOf>(parent)
    .ForEach(chunk =>
    {
        var positions = chunk.Components<Position>();
        var velocities = chunk.Components<Velocity>();

        for (var i = 0; i < chunk.Count; i++)
        {
            positions[i].X += velocities[i].X;
            positions[i].Y += velocities[i].Y;
        }
    });
```

## V2 Quick Start

```csharp
using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;

var world = new WorldV2(new WorldV2Config
{
    EntityCapacity = 100_000,
    ComponentTypeCapacity = 256,
    ChunkCapacity = 256,
    CommandCapacity = 200_000,
    CommandPayloadCapacityPerType = 200_000,
    QueryResultCapacity = 100_000
});

using var commands = world.CreateCommandStream();
var e = commands.CreateEntity();
commands.Set(e, new Position { X = 1, Y = 2 });
world.Playback(commands);

var handle = world.Compile<PositionQuery>();
using var cursor = world.Execute(handle);
if (cursor.MoveNext())
{
    var entities = cursor.Current;
    for (var i = 0; i < entities.Length; i++)
    {
        ref var position = ref cursor.Get<Position>(i);
        position.X += 10;
    }
}

readonly struct PositionQuery : ICompiledQuerySpec
{
    public void Build(ref QueryBuilder builder) => builder.All<Position>();
}

struct Position
{
    public float X;
    public float Y;
}
```

## API Reference

### World

| Member                                                                                                                                           | Description                                                                                                                                                   |
|--------------------------------------------------------------------------------------------------------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `Spawn()` / `Spawn<T1..T4>(...)`                                                                                                                 | Creates an entity in the empty archetype or inferred archetype from initial components.                                                                       |
| `Despawn(Entity)`                                                                                                                                | Removes entity and recycles id/version.                                                                                                                       |
| `Has<T>(Entity)` / `Get<T>(Entity)` / `TryGet<T>(Entity, out T)`                                                                                 | Component lookups.                                                                                                                                            |
| `Add<T>(Entity)` / `Add<T>(Entity, in T)` / `Set<T>(Entity, in T)` / `Remove<T>(Entity)`                                                         | Component mutation APIs.                                                                                                                                      |
| `Add<TRelation>(Entity source, Entity target)`                                                                                                   | Adds a relationship edge using target-parameterized relation ids.                                                                                             |
| `Query()` / `Query<T1..T4>()` / `Query(Archetype)`                                                                                               | Builds cached chunk queries.                                                                                                                                  |
| `Query<TQuery>()`                                                                                                                                | Builds a query from a `[Query]` definition struct implementing `IQuery`.                                                                                      |
| `SetResource<T>(T)` / `GetResource<T>()`                                                                                                         | Singleton/resource storage.                                                                                                                                   |
| `Observe<T>(Action<Entity,T>)` / `ObserveAdd<T>(OnAddObserver<T>)` / `ObserveSet<T>(OnSetObserver<T>)` / `ObserveRemove<T>(OnRemoveObserver<T>)` | Subscribes to component lifecycle hooks dispatched during `CommandBuffer` playback; keep/dispose the returned `IDisposable` subscription to control lifetime. |
| `AddSystem(ISystem, Stage)` / `AddSystem<TSystem>(Stage)`                                                                                        | Adds systems to stage pipeline.                                                                                                                               |
| `Tick(float)` / `FixedTick(float)` / `LateTick(float)` / `RunPhase(SystemLoopPhase, float)`                                                      | Runs systems for the selected loop phase by stage with sync-point command playback.                                                                           |
| `CreateCommandBuffer()`                                                                                                                          | Creates manual deferred mutation buffer.                                                                                                                      |
| `GetDiagnostics()`                                                                                                                               | Returns per-archetype and world-level chunk/entity/memory diagnostics snapshot.                                                                               |
| `Serialize()` / `Serialize(Stream)` / `World.Deserialize(byte[])` / `World.Deserialize(byte[], SnapshotDeserializationOptions)`                  | Snapshot round-trip using the `BZEC` v1 binary format with configurable deserialization policy.                                                               |

### WorldV2

| Member                                                                 | Description                                                                                                      |
|------------------------------------------------------------------------|------------------------------------------------------------------------------------------------------------------|
| `WorldV2(WorldV2Config)`                                               | Creates a fixed-capacity world with explicit budgets.                                                            |
| `CreateCommandStream()` / `BeginCommands()`                            | Creates a reusable deferred command stream for structural mutations.                                             |
| `Playback(CommandStream)`                                               | Applies deferred commands; rejects streams from other worlds.                                                    |
| `Compile<TSpec>()`                                                      | Compiles and caches a query plan from `ICompiledQuerySpec`.                                                      |
| `Execute<TSpec>(QueryHandle<TSpec>)`                                   | Executes a compiled query and returns a `QueryCursor` over matching entities.                                    |
| `ForEach<TSpec,T...>(QueryHandle<TSpec>, ...)`                          | Executes compiled-query hot-path loops directly without cursor/result materialization.                            |
| `Run<TSpec,TJob,T...>(QueryHandle<TSpec>, TJob)`                        | Executes compiled-query struct jobs (`IForEach`) to avoid delegate allocations on hot loops.                     |
| `Get<T>(Entity)` / `TryGet<T>(Entity,out T)` / `TryGetManaged<T>(...)` | Fast unmanaged component access plus explicit managed-lane access.                                               |
| `Has<T>(Entity)` / `IsAlive(Entity)`                                   | Entity/component presence checks.                                                                                |
| `Reset()`                                                               | Clears entities/components while retaining allocated buffers and compiled plans for reuse.                        |
| `GetDiagnostics()`                                                      | Returns arena diagnostics (entity slots, component type catalog usage, query result buffer high-water marks).   |

### WorldV2Config

| Member | Description |
|---|---|
| `EntityCapacity` | Maximum alive + recyclable entity slots. |
| `ComponentTypeCapacity` | Maximum registered component types. |
| `ChunkCapacity` | Rows per archetype chunk in the V2 chunked storage backend. |
| `CommandCapacity` | Maximum commands recorded in one `CommandStream`. |
| `CommandPayloadCapacityPerType` | Maximum payload entries per component type in one stream. |
| `QueryResultCapacity` | Maximum entities materialized by one `Execute`. |
| `OverflowPolicy` | Overflow behavior (`FailFast` or `DropNewest`). |

### CommandStream (WorldV2)

| Member | Description |
|---|---|
| `CreateEntity()` / `CreateEntity<T>(in T)` | Defers entity creation (optional single-component fused create command). |
| `Set<T>(Entity, in T)` / `SetManaged<T>(Entity, in T)` | Defers component writes for unmanaged or managed-lane components. |
| `Remove<T>(Entity)` / `Destroy(Entity)` | Defers component removal and entity destruction. |
| `GetDiagnostics()` | Per-stream command capacity usage, high-watermark, and overflow counters. |

### QueryCursor (WorldV2)

| Member | Description |
|---|---|
| `MoveNext()` / `Current` | Advances to and exposes the current result batch of entities. |
| `Get<T>(int index)` | Gets a mutable unmanaged component reference for one matched entity. |
| `ForEach<T1>(...)` / `ForEach<T1,T2>(...)` / `ForEach<T1,T2,T3>(...)` | Sequential no-allocation hot-path iteration helpers with `ref/in` component access. |
| `Run<TJob,T...>(TJob)` | Struct-job iteration (`IForEach`) over matched entities to avoid delegate allocations. |

### Query

| Member                                                                                | Description                                                                                              |
|---------------------------------------------------------------------------------------|----------------------------------------------------------------------------------------------------------|
| `All<T>()`                                                                            | Required component filter.                                                                               |
| `None<T>()`                                                                           | Excluded component filter.                                                                               |
| `Any<T1,T2>()` / `Any(params Type[])`                                                 | At least one component must exist.                                                                       |
| `Optional<T>()`                                                                       | Optional component availability for chunk access.                                                        |
| `Changed<T>()`                                                                        | Includes chunks changed in the current version window.                                                   |
| `Related<TRelation>(Entity target)`                                                   | Relationship target filter (`Entity.Wildcard` for any target); non-wildcard target must be alive.        |
| `ForEach(...)` / `ForEach(job)` / `ForEach<TJob,T...>(TJob)` / `ForEachParallel(...)` | Serial, source-generated job-style, explicit generic job-style (arity 1-4), or parallel chunk iteration. |
| `ChunkView.OptionalComponents<T>()`                                                   | Optional component span; returns `Span<T>.Empty` when missing in the current chunk.                      |

### ISystem

`ISystem` supports:

- `OnCreate(World)` (optional)
- `OnDestroy(World)` (optional)
- `Update(IWorld, in SystemContext)` (required)
- `LoopPhase`, `Stage`, and `UpdateSettings` metadata (optional overrides)
- `[Reads<T>]`, `[Writes<T>]`, and `[Exclusive]` attributes for scheduler metadata

## Design Notes

- Archetypes are cached by sorted component type ids.
- Chunk columns use aligned unmanaged buffers for unmanaged component structs and managed arrays for structs containing references.
- Structural changes move entities between archetypes and maintain dense chunk packing.
- Command buffers are flushed at sync points during world updates.
- Command buffers retain internal recording buffers between playbacks to reduce GC churn in repeated burst workloads.
- `WorldV2.CommandStream` rents command and payload buffers from array pools, uses fixed component-type-indexed payload stores, and clears managed payload references between playbacks.
- `WorldV2.ForEach<TSpec,T...>` provides direct compiled-query loops for tighter sequential update paths than cursor materialization.
- `WorldV2.Run<TSpec,TJob,T...>` and `QueryCursor.Run<TJob,T...>` support struct jobs for allocation-free hot loops without delegate captures.
- `WorldV2` stores entities in archetype chunks (columnar per-archetype arrays) and resolves structural changes through archetype transitions.
- `QueryCursor` iterates matched chunks directly for `ForEach/Run` and materializes `Current` entities lazily only when requested.
- Observer callbacks are dispatched only during command buffer playback, keeping direct mutation calls side-effect free.
- `ObserveAdd` fires for structural component attachments; `ObserveSet` fires when an existing component value is replaced.
- Observer registrations are weakly retained by the world; dropping/collecting the subscription token automatically unsubscribes.
- Query matching is cached and incrementally updated when new archetypes are created.
- Query cache is size-bounded and LRU-evicted to avoid unbounded memory growth.
- `World.Clear()` fully resets archetype caches/transition graphs to the empty-archetype baseline, preventing retained archetype growth across repeated clear cycles.
- Resources are stored separately from entity archetypes and disposed when replaced and when the world is disposed (`Dispose` / `DisposeAsync`) if they implement `IDisposable` or `IAsyncDisposable`.
- `World.Dispose()` / `DisposeAsync()` attempt all cleanup steps (system teardown, chunk/resource cleanup), aggregate failures, and still mark the world as disposed.
- Relationship filters use target-parameterized synthetic component ids.
- `Query.Related<TRelation>(target)` does not allocate synthetic relationship ids on query creation and stale related queries do not recreate ids for dead targets.
- Despawning an entity removes incoming relationships targeting that entity and recycles released relationship ids.
- Empty chunks are compacted/released after structural removals to reduce retained memory.
- Calling any public `World` API after `Dispose()` throws `ObjectDisposedException`.
- Re-entrant world updates (e.g., calling `Tick` from inside a system update) are rejected with `InvalidOperationException`.
- `World.Deserialize` validates untrusted payloads with strict count/length limits, runtime type safety checks, and optional reference-resource allowlists.
- Systems without declared access metadata are treated as exclusive and scheduled serially by default.

## Build

```bash
dotnet build bezoro.framework.sln
dotnet test tests/Bezoro.ECS.Tests/Bezoro.ECS.Tests.csproj
```

## Target Frameworks

- `.NET 9.0`
- `.NET Standard 2.1`
