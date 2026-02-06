# Bezoro.ECS

Bezoro.ECS is an archetype-based Entity Component System focused on staged system execution, chunk queries, and deferred structural changes.

## Types

| Type | Description |
|---|---|
| `World` | ECS root for entities, components, resources, systems, queries, and snapshots. |
| `Entity` | Versioned world-scoped handle (`Id`, `Version`) with `Entity.None` and `Entity.Wildcard`. |
| `Archetype` | Exact component-set storage identity backed by chunked columns. |
| `Query` | Cached archetype query with `All/None/Any/Optional/Changed/Related` filters. |
| `IQuery` | Source-generated query definition contract for `world.Query<TQuery>()` entrypoints. |
| `ChunkView` | Span-based access to entities and component columns in a chunk. |
| `CommandBuffer` | Deferred structural changes (`Create/Destroy/Add/Set/Remove`) for safe playback. |
| `IForEach<T1,T2>` | Job-style query executor contract for writable/read-only component iteration. |
| `OnAddObserver<T>` / `OnRemoveObserver<T>` | Typed observer delegates for add/remove hooks with `ref`/`in` semantics. |
| `SystemContext` | Per-system execution context (`DeltaTime`, `Stage`, `Commands`). |
| `Stage` | System stage ordering: `Input`, `PreUpdate`, `Update`, `PostUpdate`, `Render`. |
| `ISystem` | System contract with optional lifecycle hooks and stage metadata. |
| `IWorld` | Restricted world contract for systems. |

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

| Member | Description |
|---|---|
| `Spawn()` / `Spawn<T1..T4>(...)` / `CreateEntity()` | Creates an entity in the empty archetype or inferred archetype from initial components. |
| `Despawn(Entity)` / `DestroyEntity(Entity)` | Removes entity and recycles id/version. |
| `Has<T>(Entity)` / `Get<T>(Entity)` / `TryGet<T>(Entity, out T)` | Component lookups. |
| `Add<T>(Entity)` / `Add<T>(Entity, in T)` / `Set<T>(Entity, in T)` / `Remove<T>(Entity)` | Component mutation APIs. |
| `Add<TRelation>(Entity source, Entity target)` | Adds a relationship edge using target-parameterized relation ids. |
| `Query()` / `Query<T1..T4>()` / `Query(Archetype)` | Builds cached chunk queries. |
| `Query<TQuery>()` | Builds a query from a `[Query]` definition struct implementing `IQuery`. |
| `SetResource<T>(T)` / `GetResource<T>()` | Singleton/resource storage. |
| `Observe<T>(Action<Entity,T>)` / `ObserveAdd<T>(OnAddObserver<T>)` / `ObserveRemove<T>(OnRemoveObserver<T>)` | Subscribes to component lifecycle hooks; returns `IDisposable` subscription. |
| `AddSystem(ISystem, Stage)` / `AddSystem<TSystem>(Stage)` / `RegisterSystem(ISystem)` | Adds systems to stage pipeline. |
| `Update(float)` | Runs systems by stage with sync-point command playback. |
| `CreateCommandBuffer()` | Creates manual deferred mutation buffer. |
| `Serialize()` / `World.Deserialize(byte[])` | Snapshot round-trip (`net9.0` only). |

### Query

| Member | Description |
|---|---|
| `All<T>()` | Required component filter. |
| `None<T>()` | Excluded component filter. |
| `Any<T1,T2>()` | At least one component must exist. |
| `Optional<T>()` | Optional component availability for chunk access. |
| `Changed<T>()` | Includes chunks changed in the current version window. |
| `Related<TRelation>(Entity target)` | Relationship target filter (`Entity.Wildcard` for any target). |
| `ForEach(...)` / `ForEach<TJob,T1,T2>(TJob)` / `ForEachParallel(...)` | Serial, job-style, or parallel chunk iteration. |
| `ChunkView.OptionalComponents<T>()` | Optional component span; returns `Span<T>.Empty` when missing in the current chunk. |

### ISystem

`ISystem` supports:

- `OnCreate(World)` (optional)
- `OnDestroy(World)` (optional)
- `Update(IWorld, in SystemContext)` (required)
- `Stage`, `UpdateSettings`, and `Accesses` metadata (optional overrides)

## Design Notes

- Archetypes are cached by sorted component type ids.
- Chunk columns use aligned unmanaged buffers for unmanaged component structs and managed arrays for structs containing references.
- Structural changes move entities between archetypes and maintain dense chunk packing.
- Command buffers are flushed at sync points during world updates.
- Query matching is cached and incrementally updated when new archetypes are created.
- Resources are stored separately from entity archetypes.
- Relationship filters use target-parameterized synthetic component ids.

## Build

```bash
dotnet build bezoro.framework.sln
dotnet test tests/Bezoro.ECS.Tests/Bezoro.ECS.Tests.csproj
```

## Target Frameworks

- `.NET 9.0`
- `.NET Standard 2.1`
