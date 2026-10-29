# Streaming System

ECS distance-based streaming that toggles entity state using hysteresis and round-robin processing.

## Types

| Type                         | Description                                                       |
|------------------------------|-------------------------------------------------------------------|
| `Position`                   | Canonical movement position component consumed by streaming       |
| `StreamState`                | Entity component with `IsStreamedIn` state                        |
| `StreamingConfig`            | Resource with thresholds, reference position, and per-tick budget |
| `StreamingStateChangedEvent` | Event payload emitted on transitions                              |
| `StreamingEventsResource`    | Queue resource of streaming transition events                     |
| `StreamingSystem`            | `ISystem` that evaluates `Position + StreamState` each tick       |

## Quick start

```csharp
using System.Numerics;
using Bezoro.ECS.Services;
using Bezoro.GameSystems.MovementSystem.Types;
using Bezoro.GameSystems.StreamingSystem.Services;
using Bezoro.GameSystems.StreamingSystem.Types;

using var world = new World();
var system = new StreamingSystem();
world.AddSystem(system);

world.SetResource(new StreamingConfig
{
    ReferencePosition = new Vector3(0f, 0f, 0f),
    StreamInDistance = 100f,
    StreamOutDistance = 120f,
    MaxEntitiesPerTick = 50
});

var entity = world.Spawn(
    new Position { X = 10f, Y = 0f, Z = 0f },
    new StreamState { IsStreamedIn = false }
);

world.Tick(0f);
var state = world.Read<StreamState>(entity);
```

## API reference

### StreamingConfig

| Member               | Description                                              |
|----------------------|----------------------------------------------------------|
| `ReferencePosition`  | Reference point used for distance checks                 |
| `StreamInDistance`   | Threshold to transition to streamed-in                   |
| `StreamOutDistance`  | Threshold to transition to streamed-out                  |
| `MaxEntitiesPerTick` | Round-robin budget per update (`<= 0` means process all) |

### StreamingSystem

| Member        | Description                                          |
|---------------|------------------------------------------------------|
| `Changed`     | Raised on any transition                             |
| `StreamedIn`  | Raised when an entity streams in                     |
| `StreamedOut` | Raised when an entity streams out                    |
| `Update`      | Evaluates `Position + StreamState` and updates state |

## Runtime notes

- Uses squared-distance comparisons to avoid square root cost.
- Applies hysteresis (`StreamOutDistance >= StreamInDistance`) to prevent boundary flicker.
- Maintains a round-robin cursor in `StreamingRuntimeState` so work is distributed over ticks.
- Publishes transition data to `StreamingEventsResource` for deterministic consumption.

## Ownership and integration

Configuration, component writes, event-queue draining, and world disposal belong to the world thread. Callbacks run on that thread; this subsystem does not marshal Unity or Godot scene access. The entity budget counts evaluations, not elapsed time, and transitions report a state decision rather than loading or unloading engine assets themselves.

See [the module README](../README.md) for targets and engine integration. Run the .NET 10 tests with:

```bash
dotnet test tests/Bezoro.GameSystems.Tests/Bezoro.GameSystems.Tests.csproj -c Release -f net10.0 --filter "FullyQualifiedName~StreamingSystem"
```
