# Bezoro.GameSystems

ECS-native gameplay systems built on top of `Bezoro.ECS`, `Bezoro.Core`, and `Bezoro.Events`. The assembly groups reusable runtime pipelines that can be composed into game loops without coupling them to any specific engine.

## Included Systems

- `ActivationSystem`: deferred callback activation with priority ordering and per-tick budget control
- `HealthSystem`: health state, damage, healing, and lifecycle-oriented health flow
- `InputSystem`: input command ingestion patterns for ECS-driven gameplay
- `StreamingSystem`: streaming-oriented system composition for chunk or world activation workflows
- `TimerSystem`: timer components and tick-based timer progression

## Quick Start

```csharp
using Bezoro.ECS.Services;
using Bezoro.GameSystems.ActivationSystem.Extensions;
using Bezoro.GameSystems.ActivationSystem.Types;

var world = new World();
world.SetResource(new ActivationConfig(maxActivationsPerTick: 4));
world.AddActivationPipeline();
```

## Project Notes

- This assembly is the packaged runtime surface for the gameplay systems under `src/Bezoro.GameSystems`.
- Individual subsystem documentation lives next to each subsystem folder:
- `ActivationSystem/README.md`
- `HealthSystem/README.md`
- `InputSystem/README.md`
- `StreamingSystem/README.md`
- `TimerSystem/README.md`

## Dependencies

- [Bezoro.Core](../Bezoro.Core/README.md)
- [Bezoro.ECS](../Bezoro.ECS/README.md)
- [Bezoro.Events](../Bezoro.Events/README.md)
