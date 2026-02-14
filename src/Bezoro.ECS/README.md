# Bezoro.ECS
High-performance ECS runtime centered on `World`, `CommandStream`, and compiled queries.

## Types
| Type                                                                         | Description                                                                                                     |
|------------------------------------------------------------------------------|-----------------------------------------------------------------------------------------------------------------|
| `World`                                                                      | Default ECS runtime (`Services/World.cs`) for entity/component storage, system scheduling, and query execution. |
| `WorldConfig`                                                                | Fixed-capacity runtime configuration (entity/component/query/command capacities and overflow policy).           |
| `WorldOptions`                                                               | Compatibility configuration surface (maps to `WorldConfig` for migration scenarios).                            |
| `Entity`                                                                     | Stable entity handle (`id`, `version`).                                                                         |
| `CommandStream`                                                              | Fixed-capacity deferred structural mutation stream (`CreateEntity/Set/Remove/Destroy`) with explicit playback.  |
| `QueryBuilder` / `ICompiledQuerySpec` / `QueryHandle<TSpec>` / `QueryCursor` | No-allocation compiled query pipeline and iteration APIs.                                                       |
| `ISystem` / `SystemContext` / `SystemUpdateSettings`                         | System contract and scheduling context for `Tick`/`FixedTick`/`LateTick` execution.                             |

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
| Member                                                                        | Description                                                  |
|-------------------------------------------------------------------------------|--------------------------------------------------------------|
| `Spawn`, `Despawn`, `Add`, `Set`, `Remove`, `Get`, `TryGet`, `Has`, `IsAlive` | Core entity/component operations.                            |
| `CreateCommandStream`, `Playback`                                             | Deferred mutation recording and deterministic apply stage.   |
| `Compile<TSpec>`, `Execute<TSpec>`, `ForEach(...)`, `Run(...)`                | Compiled query plan creation and hot-path iteration helpers. |
| `AddSystem`, `Tick`, `FixedTick`, `LateTick`, `RunPhase`                      | System registration and phase-based scheduling.              |
| `GetResource<T>`, `SetResource<T>`                                            | Type-indexed resource storage.                               |

## Design Notes
- Default runtime is `src/Bezoro.ECS/Services/World.cs`.
- The hot path is data-oriented with fixed-capacity buffers and explicit command playback to minimize allocations.
- Compiled queries are intended for sequential, cache-friendly component access in gameplay loops.
