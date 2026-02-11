# HealthSystem

ECS-native health simulation with deferred mutation requests and event queue integration.

## Types

| Type                    | Description                                                                 |
|-------------------------|-----------------------------------------------------------------------------|
| `Health`                | ECS component for base + excess health values.                              |
| `HealthMutationRequest` | Deferred mutation command component (`Damage`, `DirectDamage`, `Heal`, `IncreaseHealth`, `SetMax`). |
| `HealthMutationKind`    | Request operation enum consumed by `HealthSystem`.                          |
| `HealthSystem`          | ECS `ISystem` that consumes requests and mutates `Health`.                  |
| `HealthChangedEvent`    | Event payload with before/after values and signed deltas.                   |
| `HealthEventsResource`  | Pull-based world event queue for health mutation events.                    |
| `HealthWorldExtensions` | Queue helpers for damage/heal variants and max-health updates.               |

## Quick Start

```csharp
using Bezoro.ECS.Services;
using Bezoro.GameSystems.HealthSystem.Extensions;
using Bezoro.GameSystems.HealthSystem.Services;
using Bezoro.GameSystems.HealthSystem.Types;

var world = new World();
var healthSystem = new HealthSystem();
world.AddSystem(healthSystem);

var entity = world.Spawn(new Health(max: 100u, current: 100u, excessCurrent: 10u, excessMax: 25u));

world.QueueHealthDamage(entity, 15u);
world.QueueHealthHeal(entity, 5u);
world.QueueHealthIncreaseHealth(entity, 20u);
world.Tick(0f);

var health = world.Get<Health>(entity);
Console.WriteLine($"{health.Current}/{health.Max} + {health.ExcessCurrent} excess");

ref var events = ref world.GetResource<HealthEventsResource>();
while (events.TryDequeue(out var evt))
{
    Console.WriteLine($"{evt.Kind}: dCurrent={evt.DeltaCurrent}, dExcess={evt.DeltaExcess}");
}
```

## API Reference

### Health

| Member             | Description                                              |
|--------------------|----------------------------------------------------------|
| `Current`          | Base health value.                                       |
| `Max`              | Base health cap.                                         |
| `ExcessCurrent`    | Excess health value (shield/overheal pool).              |
| `ExcessMax`        | Excess health cap.                                       |
| `EffectiveCurrent` | `Current + ExcessCurrent`, saturated at `uint.MaxValue`. |
| `BasePercentage`   | Base pool percentage.                                    |
| `ExcessPercentage` | Excess pool percentage.                                  |
| `TotalPercentage`  | Combined base + excess percentage.                       |

### MaxValueUpdateMode

| Value                | Description                                                |
|----------------------|------------------------------------------------------------|
| `ClampCurrent`       | Clamp current health to new max (default).                 |
| `PreservePercentage` | Scale current health proportionally to maintain percentage |

### HealthWorldExtensions

| Method                                                        | Description                         |
|---------------------------------------------------------------|-------------------------------------|
| `QueueHealthDamage(IWorld, Entity, uint)`                     | Queues excess-first damage.         |
| `QueueHealthDirectDamage(IWorld, Entity, uint)`               | Queues direct base-health damage.   |
| `QueueHealthHeal(IWorld, Entity, uint)`                       | Queues base-only heal (no overflow).|
| `QueueHealthIncreaseHealth(IWorld, Entity, uint)`             | Queues heal with overflow to excess.|
| `QueueSetHealthMax(IWorld, Entity, uint, MaxValueUpdateMode)` | Queues a max-health change request. |

## Design Notes

- Mutations are deferred via request entities so gameplay code can enqueue operations safely.
- `HealthSystem` consumes requests in queue order and despawns request entities after processing.
- Damage supports both excess-first (`Damage`) and base-only (`DirectDamage`).
- Healing supports both base-only (`Heal`) and overflow-to-excess (`IncreaseHealth`).
- Events are emitted both through `HealthSystem.Changed` and `HealthEventsResource`.
