# Bezoro.ECS

Bezoro.ECS is an archetype-based Entity Component System focused on staged system execution, chunk queries, and deferred structural changes.

## Types

| Type                                        | Description                                                                                          |
|---------------------------------------------|------------------------------------------------------------------------------------------------------|
| `World`                                     | ECS root for entities, components, resources, systems, queries, and snapshots.                       |
| `Entity`                                    | 8-byte versioned handle (`Id`, `Version`) with `Entity.None` and `Entity.Wildcard`.                  |
| `Archetype`                                 | Exact component-set storage identity backed by chunked columns.                                      |
| `Query`                                     | Cached archetype query with `All/None/Any/Optional/Changed/Related` filters.                         |
| `IQuery`                                    | Source-generated query definition contract for `world.Query<TQuery>()` entrypoints.                  |
| `ChunkView`                                 | Span-based access to entities and component columns in a chunk.                                      |
| `CommandBuffer`                             | Deferred structural changes (`Create/Destroy/Add/Set/Remove`) for safe playback.                     |
| `IForEach<T...>`                            | Job-style query executor contracts for arity 1-4 (`ref` first component, `in` remaining components). |
| `[SplitFields]` / `[SplitGroup]`            | Opt-in split storage annotations consumed by source-generated split helpers.                         |
| `OnAddObserver<T>` / `OnRemoveObserver<T>`  | Typed observer delegates for add/remove hooks with `ref`/`in` semantics.                             |
| `WorldDiagnostics` / `ArchetypeDiagnostics` | Snapshot diagnostics for archetype/chunk/entity memory usage.                                        |
| `SystemContext`                             | Per-system execution context (`DeltaTime`, `Stage`, `Commands`).                                     |
| `SystemLoopPhase`                           | Host loop routing: `Tick`, `FixedTick`, `LateTick`.                                                  |
| `Stage`                                     | System stage ordering: `Input`, `PreTick`, `Tick`, `PostTick`, `Render`.                             |
| `SnapshotDeserializationOptions`            | Snapshot type-resolution and reference-resource allowlist options for secure deserialization.        |
| `ISystem`                                   | System contract with optional lifecycle hooks and stage metadata.                                    |
| `IWorld`                                    | Restricted world contract for systems.                                                               |

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

## API Reference

### World

| Member                                                                                                                          | Description                                                                                                             |
|---------------------------------------------------------------------------------------------------------------------------------|-------------------------------------------------------------------------------------------------------------------------|
| `Spawn()` / `Spawn<T1..T4>(...)`                                                                                                | Creates an entity in the empty archetype or inferred archetype from initial components.                                 |
| `Despawn(Entity)`                                                                                                               | Removes entity and recycles id/version.                                                                                 |
| `Has<T>(Entity)` / `Get<T>(Entity)` / `TryGet<T>(Entity, out T)`                                                                | Component lookups.                                                                                                      |
| `Add<T>(Entity)` / `Add<T>(Entity, in T)` / `Set<T>(Entity, in T)` / `Remove<T>(Entity)`                                        | Component mutation APIs.                                                                                                |
| `Add<TRelation>(Entity source, Entity target)`                                                                                  | Adds a relationship edge using target-parameterized relation ids.                                                       |
| `Query()` / `Query<T1..T4>()` / `Query(Archetype)`                                                                              | Builds cached chunk queries.                                                                                            |
| `Query<TQuery>()`                                                                                                               | Builds a query from a `[Query]` definition struct implementing `IQuery`.                                                |
| `SetResource<T>(T)` / `GetResource<T>()`                                                                                        | Singleton/resource storage.                                                                                             |
| `Observe<T>(Action<Entity,T>)` / `ObserveAdd<T>(OnAddObserver<T>)` / `ObserveRemove<T>(OnRemoveObserver<T>)`                    | Subscribes to component lifecycle hooks dispatched during `CommandBuffer` playback; returns `IDisposable` subscription. |
| `AddSystem(ISystem, Stage)` / `AddSystem<TSystem>(Stage)`                                                                       | Adds systems to stage pipeline.                                                                                         |
| `Tick(float)` / `FixedTick(float)` / `LateTick(float)` / `RunPhase(SystemLoopPhase, float)`                                     | Runs systems for the selected loop phase by stage with sync-point command playback.                                     |
| `CreateCommandBuffer()`                                                                                                         | Creates manual deferred mutation buffer.                                                                                |
| `GetDiagnostics()`                                                                                                              | Returns per-archetype and world-level chunk/entity/memory diagnostics snapshot.                                         |
| `Serialize()` / `Serialize(Stream)` / `World.Deserialize(byte[])` / `World.Deserialize(byte[], SnapshotDeserializationOptions)` | Snapshot round-trip using the `BZEC` v1 binary format with configurable deserialization policy.                         |

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
- Observer callbacks are dispatched only during command buffer playback, keeping direct mutation calls side-effect free.
- Query matching is cached and incrementally updated when new archetypes are created.
- Query cache is size-bounded and LRU-evicted to avoid unbounded memory growth.
- Resources are stored separately from entity archetypes and disposed when replaced and when the world is disposed (`Dispose` / `DisposeAsync`) if they implement `IDisposable` or `IAsyncDisposable`.
- Relationship filters use target-parameterized synthetic component ids.
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
