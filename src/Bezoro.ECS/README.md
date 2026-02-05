# Bezoro.ECS

Bezoro.ECS is a cache-friendly, archetype-based Entity Component System designed for batch processing and parallel execution.

## Types

| Type                   | Description                                                                |
|------------------------|----------------------------------------------------------------------------|
| `World`                | Core entry point for entities, components, archetypes, and system updates. |
| `Archetype`            | Fixed set of component types stored together in chunked SoA layout.        |
| `CommandBuffer`        | Records deferred structural changes.                                       |
| `Query`                | Chunked query over entities with required components.                      |
| `ChunkView`            | Per-chunk view exposing entities and component spans.                      |
| `SystemUpdateSettings` | Configures system update frequency.                                        |
| `ComponentAccess`      | Declares system read/write component access for scheduling.                |
| `Entity`               | Stable entity handle with id + version.                                    |
| `IWorld`               | Read/query surface for systems.                                            |
| `ISystem`              | Update contract for systems.                                               |

## Quick Start

```csharp
using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Services;
using Bezoro.ECS.Types;

var world = new World();

public struct Position : IComponent { public float X; public float Y; }
public struct Velocity : IComponent { public float X; public float Y; }

// Create entity and add components
var entity = world.CreateEntity();
world.AddComponent(entity, new Position { X = 1, Y = 2 });
world.AddComponent(entity, new Velocity { X = 0.5f, Y = 0.25f });

// Query Position + Velocity in chunked batches
foreach (var chunk in world.Query().With<Position>().With<Velocity>())
{
    var positions = chunk.Components<Position>();
    var velocities = chunk.Components<Velocity>();
    for (int i = 0; i < chunk.Count; i++)
    {
        positions[i].X += velocities[i].X;
        positions[i].Y += velocities[i].Y;
    }
}

// Systems with deferred structural changes
public sealed class SpawnSystem : ISystem
{
    public SystemUpdateSettings UpdateSettings => SystemUpdateSettings.Fixed(0.5f);
    public ComponentAccess[] Accesses => [ComponentAccess.Read<Position>()];

    public void Update(IWorld world, in SystemContext context)
    {
        var entity = context.Commands.CreateEntity();
        context.Commands.AddComponent(entity, new Position { X = 0, Y = 0 });
    }
}

world.RegisterSystem(new SpawnSystem());
world.Update(1f / 60f);
```

## API Reference

### `IWorld`

| Member                              | Description                                                 |
|-------------------------------------|-------------------------------------------------------------|
| `IsAlive(Entity)`                   | Checks whether an entity handle is valid.                   |
| `HasComponent<T>(Entity)`           | Checks for component presence.                              |
| `TryGetComponent<T>(Entity, out T)` | Tries to read a component value.                            |
| `GetComponent<T>(Entity)`           | Reads a component value or throws if missing.               |
| `SetComponent<T>(Entity, in T)`     | Sets or adds a component value.                             |
| `Query()`                           | Creates a chunked query builder.                            |
| `Query(Archetype)`                  | Creates a chunked query builder restricted to an archetype. |

### `ISystem`

| Member                             | Description                                   |
|------------------------------------|-----------------------------------------------|
| `UpdateSettings`                   | Update frequency configuration.               |
| `Accesses`                         | Component access requirements for scheduling. |
| `Update(IWorld, in SystemContext)` | System update entry point.                    |

## Archetypes & Queries

- Archetypes represent exact component sets and store data in chunked SoA arrays.
- Queries iterate archetypes that contain required components.
- Use `World.Query(archetype)` to target a specific archetype only.

## Command Buffers

- Structural changes (create/destroy/add/remove) are recorded in a `CommandBuffer`.
- Buffers are applied after system execution to keep iteration stable and parallel-safe.

## Scheduling & Parallelism

- Systems declare read/write access via `ComponentAccess`.
- The scheduler batches compatible systems and runs them in parallel.
- Use `SystemUpdateSettings.Fixed(intervalSeconds)` to control update frequency.

## Design Notes

- Chunked SoA storage keeps `Position` and `Velocity` arrays contiguous for fast scans.
- Structural changes move entities between archetypes; command buffers defer those moves.
- Parallel updates are safe when systems declare accurate access patterns.

## Target Frameworks

- `.NET 9.0`
- `.NET Standard 2.1`
