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

## Quick Start

```csharp
var world = new World();
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
var state = world.Get<StreamState>(entity);
```

## API Reference

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

## Design Notes

- Uses squared-distance comparisons to avoid square root cost.
- Applies hysteresis (`StreamOutDistance >= StreamInDistance`) to prevent boundary flicker.
- Maintains a round-robin cursor in `StreamingRuntimeState` so work is distributed over ticks.
- Publishes transition data to `StreamingEventsResource` for deterministic consumption.
