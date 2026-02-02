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
| `HealthRegenService`   | Timer-based batch regen with zero per-tick allocations       |
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

`HealthRegenService` provides timed health-over-time regeneration using a single background timer with batch processing and zero per-tick allocations. Default tick frequency is 20ms (50 ticks/second).

### Types

| Type                  | Description                                                  |
|-----------------------|--------------------------------------------------------------|
| `IHealthRegenService` | Regen service contract                                       |
| `HealthRegenService`  | Timer-based batch implementation with precision delivery     |
| `RegenHandle`         | Lightweight handle to a regen effect (mirrors `TimerHandle`) |

### Quick Start

```csharp
using Bezoro.GameSystems.HealthSystem.Services;
using Bezoro.GameSystems.HealthSystem.Types;

var service = new HealthRegenService();
var health  = new Health(1000u, 200u);

// Finite regen: 50 HP/s for 4 seconds (delivers exactly 200 HP, default 20ms ticks)
var handle = service.StartRegen(health, amountPerSec: 50f, TimeSpan.FromSeconds(4));

// Stack another finite regen on top
service.AddRegen(health, amountPerSec: 10f, TimeSpan.FromSeconds(10));

// Discrete 1/s ticks: 20 HP/s for 5 seconds at 1000ms tick frequency
service.AddRegen(health, amountPerSec: 20f, TimeSpan.FromSeconds(5), tickFrequencyMs: 1000);

// Infinite regen: heal 25 HP/s indefinitely until stopped
var repeating = service.StartRepeatingRegen(health, amountPerSec: 25f);

// Stack an infinite regen alongside existing ones
service.AddRepeatingRegen(health, amountPerSec: 3f, tickFrequencyMs: 100);

// Cancel a specific regen
service.Stop(handle);

// Cancel all regens on a target
service.StopAll(health);
```

### API Reference

| Member                                                                                       | Description                                         |
|----------------------------------------------------------------------------------------------|-----------------------------------------------------|
| `int ActiveCount`                                                                            | Number of active regen effects                      |
| `bool IsActive(RegenHandle)`                                                                 | Whether a specific regen is still ticking           |
| `RegenHandle StartRegen(IHealth, float amountPerSec, TimeSpan duration, uint tickFreqMs=20)` | Replace all regens on target with a finite regen    |
| `RegenHandle AddRegen(IHealth, float amountPerSec, TimeSpan duration, uint tickFreqMs=20)`   | Stack a finite regen alongside existing ones        |
| `RegenHandle StartRepeatingRegen(IHealth, float amountPerSec, uint tickFreqMs=20)`           | Replace all regens on target with an infinite regen |
| `RegenHandle AddRepeatingRegen(IHealth, float amountPerSec, uint tickFreqMs=20)`             | Stack an infinite regen alongside existing ones     |
| `bool Stop(RegenHandle)`                                                                     | Cancel a specific regen                             |
| `void StopAll(IHealth)`                                                                      | Cancel all regens on a target                       |
| `void Update(float deltaTime)`                                                               | Advance all regens by delta time (seconds)          |
| `void Dispose()`                                                                             | Dispose the internal timer                          |
| `static CreateManual()`                                                                      | Create a service with no timer (caller-driven)      |

### Finite vs Repeating

- **Finite** (`StartRegen`/`AddRegen`) — Takes a `TimeSpan duration`. Ticks for `duration / tickFrequencyMs` total ticks, then stops automatically. Guarantees exact HP delivery (see below).
- **Repeating** (`StartRepeatingRegen`/`AddRepeatingRegen`) — No duration parameter. Ticks indefinitely until explicitly stopped via `Stop` or `StopAll`.
- **`Start*`** calls `StopAll(target)` before adding. **`Add*`** stacks alongside existing regens.
- **`tickFrequencyMs`** controls granularity: 20ms (default) = smooth, 1000ms = discrete 1/s ticks.

### Precision Guarantees

**Finite regens** use Bresenham-style integer distribution to deliver exactly `Round(amountPerSec * duration.TotalSeconds)` total HP. The total is precomputed as an integer and distributed evenly across ticks, eliminating floating-point accumulation errors. For example, 7 HP/s for 1 second at 20ms ticks delivers exactly 7 HP — not 6 as a naive float accumulator would.

**Repeating (infinite) regens** use a `double` accumulator (53-bit mantissa) instead of `float` (23-bit). The accumulator stays in `[0, 1)` after each subtraction, maintaining precision indefinitely.

## Design Notes

- **Excess-first damage**: `DecreaseCurrentHealthBy` drains excess health before reducing current health.
- **Restore vs Increase**: `RestoreCurrentHealthBy` never creates excess; `IncreaseCurrentHealthBy` overflows into excess.
- **Overflow-safe arithmetic**: All operations use `ulong` intermediates and saturate at `uint.MaxValue`.
- **Constructor overflow**: If `current > max` during construction, the overflow is moved to excess.
