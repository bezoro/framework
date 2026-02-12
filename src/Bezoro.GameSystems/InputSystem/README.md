# InputSystem

External input ingestion for ECS movement, with hold-last behavior and sequence-based ordering.

## Types

| Type                     | Description                                                                |
|--------------------------|----------------------------------------------------------------------------|
| `IInputCommandSink`      | External producer contract for enqueueing immutable `InputCommand` values. |
| `InputCommand`           | Immutable movement command (`ControlId`, axis values, `Sequence`).         |
| `InputCommandQueue`      | Thread-safe queue + latest-per-control snapshot resource.                  |
| `InputControl`           | Component that maps an entity to a logical input channel id.               |
| `MovementIntent`         | Component storing currently applied movement intent.                       |
| `MovementInputSettings`  | Component with per-entity movement speed and hold duration.                |
| `InputIngestionSystem`   | `FixedTick` + `Input` stage system that drains queued commands.            |
| `IntentToVelocitySystem` | `FixedTick` + `PreTick` stage system that converts intent to `Velocity`.   |
| `InputWorldExtensions`   | Convenience methods for queue resource setup and pipeline registration.    |

## Quick Start

```csharp
using Bezoro.ECS.Services;
using Bezoro.GameSystems.InputSystem.Extensions;
using Bezoro.GameSystems.InputSystem.Types;
using Bezoro.GameSystems.MovementSystem.Services;
using Bezoro.GameSystems.MovementSystem.Types;

var world = new World();
world.AddMovementInputPipeline();
world.AddSystem(new MovementSystem(), Stage.Tick);

var queue = world.GetOrCreateInputCommandQueue();

var player = world.Spawn(
    new Position(),
    new Velocity(),
    new InputControl { ControlId = 1 },
    new MovementIntent(),
    new MovementInputSettings { Speed = 5f, HoldDurationSeconds = 0.15f }
);

queue.Enqueue(new InputCommand(controlId: 1, moveX: 1f, moveY: 0f, moveZ: 0f, sequence: 1));
world.FixedTick(0.02f);
```

## API Reference

### InputCommandQueue

| Member                                         | Description                                                  |
|------------------------------------------------|--------------------------------------------------------------|
| `Enqueue(in InputCommand)`                     | Adds an external command from any thread.                    |
| `Enqueue(int,float,float,float,ulong)`         | Convenience overload for command creation + enqueue.         |
| `SimulationTimeSeconds`                        | Current simulation time used for hold-last aging.            |

### InputWorldExtensions

| Method                                | Description                                                  |
|---------------------------------------|--------------------------------------------------------------|
| `GetOrCreateInputCommandQueue(World)` | Returns existing input queue resource or creates one.        |
| `AddMovementInputPipeline(World)`     | Registers ingestion and intent systems with correct stages.  |

## Hold-Last Behavior

- Each control id keeps the most recent command by `Sequence`.
- `IntentToVelocitySystem` keeps that command active while command age is within `HoldDurationSeconds`.
- When the hold window expires without newer input, `MovementIntent` and `Velocity` are zeroed.

## Design Notes

- Producers and consumers are isolated: external threads only enqueue immutable commands.
- ECS writes happen on world update thread only.
- Stage ordering is explicit: input ingestion (`Input`) -> velocity conversion (`PreTick`) -> movement (`Tick`).
