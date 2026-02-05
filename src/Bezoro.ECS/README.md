# Bezoro.ECS

Bezoro.ECS is a cache-friendly, archetype-based Entity Component System for high-throughput, chunked, parallel updates.

## Types

| Type                   | Description                                                                             |
|------------------------|-----------------------------------------------------------------------------------------|
| `World`                | Main ECS entry point for entities, components, archetypes, queries, and system updates. |
| `WorldOptions`         | Performance options (`ChunkCapacity`, `MaxDegreeOfParallelism`).                        |
| `Archetype`            | Exact component-set identity with chunked SoA storage.                                  |
| `Entity`               | Stable handle (`Id` + `Version`).                                                       |
| `Query`                | Builder for include/exclude component queries.                                          |
| `QueryEnumerator`      | Chunk enumerator returned by `Query`.                                                   |
| `ChunkView`            | Read/write span access to chunk-local component arrays.                                 |
| `CommandBuffer`        | Deferred structural command recorder (`Create`, `Destroy`, `Add`, `Remove`, `Set`).     |
| `SystemContext`        | Per-system execution context (`DeltaTime`, `Commands`).                                 |
| `SystemUpdateSettings` | System update cadence (`EveryFrame`, fixed interval helpers).                           |
| `ComponentAccess`      | Scheduler metadata describing read/write component access.                              |
| `ComponentAccessMode`  | `ReadOnly` or `ReadWrite`.                                                              |
| `IWorld`               | Restricted world surface exposed to systems.                                            |
| `ISystem`              | ECS system contract.                                                                    |
| `IComponent`           | Marker interface for struct components.                                                 |
| `IEntity`              | Entity abstraction exposing `Id` and `Version`.                                         |

## Quick Start

```csharp
using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Options;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;

var world = new World(new WorldOptions { ChunkCapacity = 256 });

public struct Position : IComponent { public float X; public float Y; }
public struct Velocity : IComponent { public float X; public float Y; }
public struct FrozenTag : IComponent { }

var entity = world.CreateEntity();
world.AddComponent(entity, new Position { X = 1, Y = 2 });
world.SetComponent(entity, new Velocity { X = 0.5f, Y = 0.25f });

foreach (var chunk in world.Query().With<Position>().With<Velocity>().Without<FrozenTag>())
{
    var positions = chunk.Components<Position>();
    var velocities = chunk.Components<Velocity>();
    for (var i = 0; i < chunk.Count; i++)
    {
        positions[i].X += velocities[i].X;
        positions[i].Y += velocities[i].Y;
    }
}

world.Update(1f / 60f);
```

## API Reference

### `World`

| Member                                       | Description                                                                             |
|----------------------------------------------|-----------------------------------------------------------------------------------------|
| `World()` / `World(WorldOptions)`            | Creates a world with default or custom chunk/scheduler settings.                        |
| `EntityCount`                                | Count of currently alive entities.                                                      |
| `GetOrCreateArchetype(...)`                  | Gets/creates archetype identity for a component set.                                    |
| `CreateEntity()` / `CreateEntity(Archetype)` | Creates an entity in empty or specific archetype.                                       |
| `DestroyEntity(Entity)`                      | Destroys entity and recycles id with version bump.                                      |
| `IsAlive(Entity)`                            | Checks whether a handle is currently valid.                                             |
| `HasComponent<T>(Entity)`                    | Checks component presence.                                                              |
| `TryGetComponent<T>(Entity, out T)`          | Gets component if present.                                                              |
| `GetComponent<T>(Entity)`                    | Gets component or throws if missing.                                                    |
| `AddComponent<T>(Entity, in T)`              | Add-only operation. Throws if component already exists.                                 |
| `SetComponent<T>(Entity, in T)`              | Upsert operation (update existing or add missing).                                      |
| `RemoveComponent<T>(Entity)`                 | Removes component if present.                                                           |
| `Query()` / `Query(Archetype)`               | Creates query over all archetypes or one archetype.                                     |
| `CreateCommandBuffer()`                      | Creates deferred structural command buffer.                                             |
| `RegisterSystem(ISystem)`                    | Registers system for scheduler-driven updates.                                          |
| `Update(float)` / `Update()`                 | Runs system scheduler and command playback.                                             |
| `Clear()`                                    | Removes all entities and chunk data while preserving archetypes and registered systems. |

### `Query`

| Member                                     | Description                                             |
|--------------------------------------------|---------------------------------------------------------|
| `With<T>()` / `With(params Type[])`        | Adds required component types (AND semantics).          |
| `Without<T>()` / `Without(params Type[])`  | Adds excluded component types (entity must match none). |
| `ForArchetype(Archetype)`                  | Restricts the query to one archetype.                   |
| `GetEnumerator()`                          | Supports `foreach` chunk iteration.                     |
| `ForEach(Action<ChunkView>)`               | Executes action for each matching chunk (serial).       |
| `ForEachParallel(Action<ChunkView>, int?)` | Executes action for each matching chunk in parallel.    |

### `ChunkView`

| Member                          | Description                                          |
|---------------------------------|------------------------------------------------------|
| `Count`                         | Number of entities in the chunk.                     |
| `Entities`                      | `ReadOnlySpan<Entity>` for entities in `[0..Count)`. |
| `TryComponents<T>(out Span<T>)` | Gets mutable component span if chunk has that type.  |
| `Components<T>()`               | Gets mutable component span or throws if missing.    |

### `CommandBuffer`

| Member                                       | Description                                                            |
|----------------------------------------------|------------------------------------------------------------------------|
| `CreateEntity()` / `CreateEntity(Archetype)` | Records deferred entity creation.                                      |
| `DestroyEntity(Entity)`                      | Records deferred destroy.                                              |
| `AddComponent<T>(Entity, in T)`              | Records add-only component op (throws on playback if already present). |
| `SetComponent<T>(Entity, in T)`              | Records upsert component op.                                           |
| `RemoveComponent<T>(Entity)`                 | Records deferred component removal.                                    |
| `Playback()`                                 | Applies all recorded commands in order and clears the buffer.          |

Command buffer notes:

- Commands are executed in the order they were recorded.
- Temporary entities returned by `CreateEntity()` are only meaningful within that same buffer before playback.
- During `World.Update(...)`, command playback is handled by the scheduler after system execution.

### `IWorld`

`IWorld` is the system-facing restricted surface:

| Member                              | Description                                                        |
|-------------------------------------|--------------------------------------------------------------------|
| `IsAlive(Entity)`                   | Handle validity check.                                             |
| `HasComponent<T>(Entity)`           | Component presence check.                                          |
| `TryGetComponent<T>(Entity, out T)` | Safe read.                                                         |
| `GetComponent<T>(Entity)`           | Read or throw.                                                     |
| `SetComponent<T>(Entity, in T)`     | Upsert; during update, only in-place updates are allowed directly. |
| `Query()` / `Query(Archetype)`      | Chunk query access.                                                |

### `ISystem`

| Member                             | Description                                               |
|------------------------------------|-----------------------------------------------------------|
| `UpdateSettings`                   | Run frequency (`EveryFrame` or fixed interval).           |
| `Accesses`                         | Read/write declarations used for conflict-aware batching. |
| `Update(IWorld, in SystemContext)` | System execution entry point.                             |

## Lifecycle And Structural Rules

- Entity identity is `(Id, Version)`.
- Destroying or clearing invalidates previous handles through version increments.
- Structural world operations are blocked while `World.Update(...)` is running.
- During update, `SetComponent` can update an existing component in place.
- During update, adding a new component type, removing components, creating, or destroying entities must go through `CommandBuffer`.
- `Clear()` preserves archetype objects and registered systems, resets `EntityCount` to `0`, and invalidates all existing entity handles.

## Query Semantics

- `With` requirements are combined with AND semantics.
- `Without` exclusions are combined with OR semantics (if any excluded component exists, entity is filtered out).
- Query iteration is chunk-based, not entity-by-entity.
- `ChunkView` spans are direct views over internal chunk arrays; process immediately and do not cache spans past the current iteration scope.

### Example: Exclusion Query

```csharp
var moving = world.Query()
    .With<Position>()
    .With<Velocity>()
    .Without<FrozenTag>();

moving.ForEach(chunk =>
{
    var p = chunk.Components<Position>();
    var v = chunk.Components<Velocity>();
    for (var i = 0; i < chunk.Count; i++)
    {
        p[i].X += v[i].X;
        p[i].Y += v[i].Y;
    }
});
```

## Scheduling And Parallelism

- Systems are scheduled in batches based on `ComponentAccess` conflict rules.
- Systems in the same batch run in parallel up to `WorldOptions.MaxDegreeOfParallelism`.
- Each system execution receives its own `CommandBuffer`.
- Buffered commands are played back after scheduled system execution.
- Fixed-step systems use an accumulator. Catch-up backlog is intentionally capped (currently 3 ticks) to avoid unbounded spiral-of-death behavior.

## Threading Model

- Scheduler execution may run multiple systems concurrently when access declarations permit it.
- `CommandBuffer` recording is synchronized and safe for concurrent callers.
- World structural mutation is single-threaded through the update/playback flow.
- If `ForEachParallel` is used, callback code must be thread-safe.

## Design Notes

- Storage is archetype + chunk + SoA arrays for cache-efficient scans.
- Structural changes move entities between archetypes and preserve dense chunks with swap-back removal.
- Component type ids are registered once and used for fast archetype matching/indexing.

## Build And Test

```bash
dotnet build bezoro.framework.sln
dotnet test tests/Bezoro.ECS.Tests/Bezoro.ECS.Tests.csproj
```

## Target Frameworks

- `.NET 9.0`
- `.NET Standard 2.1`
