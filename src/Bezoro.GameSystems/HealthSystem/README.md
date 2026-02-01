# HealthSystem

A health management system with overflow/shield (excess health) support and safe arithmetic that prevents `uint` overflow.

## Types

| Type                   | Description                                                  |
|------------------------|--------------------------------------------------------------|
| `IHealth`              | Core health contract (current, max, percentage, damage/heal) |
| `IExcessHealth`        | Optional interface for overflow/shield mechanics             |
| `Health`               | Default sealed implementation of both interfaces             |
| `MaxHealthUpdateMode`  | Controls how current health adjusts when max changes         |
| `IHealthRegenService`  | Health-over-time regeneration service contract               |
| `HealthRegenService`   | Async-loop regen implementation with smooth 50ms ticking     |
| `RegenHandle`          | Lightweight handle to a regen effect                         |

## Quick Start

```csharp
using Bezoro.GameSystems.HealthSystem.Types;

var health = new Health(100u); // Max=100, Current=100, Excess=0

// Damage
health.DecreaseCurrentHealthBy(30u); // Current=70

// Heal (capped at max)
health.RestoreCurrentHealthBy(50u); // Current=100

// Overheal (excess/shield)
health.IncreaseCurrentHealthBy(20u); // Current=100, Excess=20

// Damage consumes excess first
health.DecreaseCurrentHealthBy(25u); // Excess=0, Current=95
```

## API Reference

### IHealth

| Member                          | Description                                    |
|---------------------------------|------------------------------------------------|
| `uint Current`                  | Current health value                           |
| `uint Max`                      | Maximum health value                           |
| `Percent Percentage`            | Current health as a percentage                 |
| `DecreaseCurrentHealthBy(uint)` | Apply damage (drains excess first)             |
| `IncreaseCurrentHealthBy(uint)` | Heal, overflow goes to excess                  |
| `RestoreCurrentHealthBy(uint)`  | Heal capped at max (no excess created)         |
| `DecreaseMaxHealthBy(uint)`     | Reduce maximum health                          |
| `IncreaseMaxHealthBy(uint)`     | Increase maximum health                        |
| `SetCurrentHealthTo(uint)`      | Set current to specific value (clamped to max) |
| `SetMaxHealthTo(uint)`          | Set maximum health                             |
| `DepleteCurrentHealth()`        | Set current health to zero                     |
| `FullyRestoreCurrentHealth()`   | Set current health to max                      |

### IExcessHealth

| Member                         | Description                    |
|--------------------------------|--------------------------------|
| `uint Excess`                  | Current excess (shield) health |
| `IncreaseExcessHealthBy(uint)` | Add excess health              |
| `DecreaseExcessHealthBy(uint)` | Remove excess health           |
| `SetExcessHealthTo(uint)`      | Set excess to specific value   |
| `ClearExcessHealth()`          | Remove all excess health       |

### MaxHealthUpdateMode

| Value                | Description                                                |
|----------------------|------------------------------------------------------------|
| `ClampCurrent`       | Clamp current health to new max (default)                  |
| `PreservePercentage` | Scale current health proportionally to maintain percentage |

```csharp
health.SetMaxHealthTo(50u, MaxHealthUpdateMode.ClampCurrent);
// If health was 80/100, it becomes 50/50 (clamped to new max)

health.SetMaxHealthTo(200u, MaxHealthUpdateMode.ClampCurrent);
// If health was 50/100, it becomes 50/200

health.SetMaxHealthTo(200u, MaxHealthUpdateMode.PreservePercentage);
// If health was 50/100 (50%), it becomes 100/200 (50%)
```

## Health Regeneration

`HealthRegenService` provides timed health-over-time regeneration using async loops with a smooth 50ms tick interval (20 ticks/second).

### Types

| Type                  | Description                                                  |
|-----------------------|--------------------------------------------------------------|
| `IHealthRegenService` | Regen service contract                                       |
| `HealthRegenService`  | Async-loop implementation with smooth ticking                |
| `RegenHandle`         | Lightweight handle to a regen effect (mirrors `TimerHandle`) |

### Quick Start

```csharp
using Bezoro.GameSystems.HealthSystem.Services;
using Bezoro.GameSystems.HealthSystem.Types;

var service = new HealthRegenService();
var health  = new Health(1000u, 200u);

// Smooth regen: 50 HP/s for 4 seconds (200 HP total, applied in 50ms increments)
var handle = service.StartRegen(health, amountPerSecond: 50u, durationSeconds: 4f);

// Stack another regen on top
service.AddRegen(health, amountPerSecond: 10u, durationSeconds: 10f);

// Discrete ticks: 100 HP total across 5 applications (1s apart)
service.AddRegen(health, totalAmount: 100u, ticks: 5u);

// Cancel a specific regen
service.Stop(handle);

// Cancel all regens on a target
service.StopAll(health);
```

### API Reference

| Member                                                                         | Description                                      |
|--------------------------------------------------------------------------------|--------------------------------------------------|
| `int ActiveCount`                                                              | Number of active regen effects                   |
| `bool IsActive(RegenHandle)`                                                   | Whether a specific regen is still ticking        |
| `RegenHandle StartRegen(IHealth, uint amountPerSecond, float durationSeconds)` | Replace all regens on target with a smooth regen |
| `RegenHandle StartRegen(IHealth, uint totalAmount, uint ticks)`                | Replace all regens with discrete-tick regen      |
| `RegenHandle AddRegen(IHealth, uint amountPerSecond, float durationSeconds)`   | Stack a smooth regen alongside existing ones     |
| `RegenHandle AddRegen(IHealth, uint totalAmount, uint ticks)`                  | Stack a discrete-tick regen alongside existing   |
| `bool Stop(RegenHandle)`                                                       | Cancel a specific regen                          |
| `void StopAll(IHealth)`                                                        | Cancel all regens on a target                    |

### Overload Semantics

- **`(amountPerSecond, durationSeconds)`** — Smooth mode. Heals `amountPerSecond` HP/s using 50ms micro-ticks for fluid visual feedback. Total HP = `amountPerSecond * durationSeconds`.
- **`(totalAmount, ticks)`** — Discrete mode. Distributes `totalAmount` evenly across `ticks` applications at 1-second intervals.
- **`StartRegen`** calls `StopAll(target)` before adding. **`AddRegen`** stacks alongside existing regens.

## Design Notes

- **Excess-first damage**: `DecreaseCurrentHealthBy` drains excess health before reducing current health.
- **Restore vs Increase**: `RestoreCurrentHealthBy` never creates excess; `IncreaseCurrentHealthBy` overflows into excess.
- **Overflow-safe arithmetic**: All operations use `ulong` intermediates and saturate at `uint.MaxValue`.
- **Constructor overflow**: If `current > max` during construction, the overflow is moved to excess.
