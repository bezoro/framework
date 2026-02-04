# HealthSystem

A health management system with optional overflow/shield (excess health) support and safe arithmetic that prevents `uint` overflow.

## Types

| Type                   | Description                                                       |
|------------------------|-------------------------------------------------------------------|
| `IDamageableHealth<T>` | Minimal health contract used by the DamageSystem                  |
| `Health`               | Immutable record struct for base health only                      |
| `HealthWithExcess`     | Immutable record struct with base + excess health                 |
| `MaxValueUpdateMode`   | Controls how current health adjusts when max changes              |
| `HealthChangedEvent`   | Event payload with before/after health values                     |
| `HealthChangeKind`     | Enum describing the operation that caused the change              |

## Quick Start

```csharp
using Bezoro.GameSystems.HealthSystem.Types;

var health = new Health(100u); // Max=100, Current=100

// Damage
health = health.DecreaseCurrentHealthBy(30u); // Current=70

// Heal (capped at max)
health = health.RestoreCurrentHealthBy(50u); // Current=100
```

```csharp
using Bezoro.GameSystems.HealthSystem.Types;

// Health with excess/shield capacity
var shielded = new HealthWithExcess(max: 100u, current: 100u, excess: 0u, excessMax: 50u);

// Overheal (excess/shield)
shielded = shielded.IncreaseCurrentHealthBy(20u); // Current=100, Excess=20

// Damage consumes excess first
shielded = shielded.DecreaseHealthBy(25u); // Excess=0, Current=95
```

## API Reference

### IDamageableHealth<T>

| Member                  | Description                                   |
|-------------------------|-----------------------------------------------|
| `uint EffectiveCurrent` | Current health used for damage calculations   |
| `ApplyDamage(uint)`     | Applies damage and returns the updated health |

### Health

| Member                          | Description                                         |
|---------------------------------|-----------------------------------------------------|
| `uint Current`                  | Current health value                                |
| `uint Max`                      | Maximum health value                                |
| `uint EffectiveCurrent`         | Effective current health (same as `Current`)        |
| `Percent BasePercentage`        | Current health as a percentage                      |
| `ApplyDamage(uint)`             | Applies damage (alias of `DecreaseCurrentHealthBy`) |
| `DecreaseCurrentHealthBy(uint)` | Apply damage (clamped to zero)                      |
| `RestoreCurrentHealthBy(uint)`  | Heal current health (capped at max)                 |
| `DecreaseMaxHealthBy(uint)`     | Reduce maximum health                               |
| `IncreaseMaxHealthBy(uint)`     | Increase maximum health                             |
| `SetCurrentHealthTo(uint)`      | Set current to specific value (clamped to max)      |
| `SetMaxHealthTo(uint)`          | Set maximum health                                  |
| `DepleteCurrentHealth()`        | Set current health to zero                          |
| `FullyRestoreCurrentHealth()`   | Set current health to max                           |

### HealthWithExcess

| Member                          | Description                                            |
|---------------------------------|--------------------------------------------------------|
| `uint ExcessCurrent`            | Current excess health                                  |
| `uint ExcessMax`                | Maximum excess health                                  |
| `uint EffectiveCurrent`         | Effective current health (base + excess, saturated)    |
| `Percent ExcessPercentage`      | Excess health as a percentage of excess max            |
| `Percent TotalPercentage`       | Combined health as a percentage of total max           |
| `ApplyDamage(uint)`             | Applies damage (alias of `DecreaseHealthBy`)           |
| `IncreaseCurrentHealthBy(uint)` | Heal current health, overflow into excess              |
| `DecreaseHealthBy(uint)`        | Apply damage to excess first, then base health         |
| `IncreaseExcessHealthBy(uint)`  | Add excess health                                      |
| `DecreaseExcessHealthBy(uint)`  | Remove excess health                                   |
| `SetExcessHealthTo(uint)`       | Set excess to specific value                           |
| `DepleteExcessHealth()`         | Remove all excess health                               |

### MaxValueUpdateMode

| Value                | Description                                                |
|----------------------|------------------------------------------------------------|
| `ClampCurrent`       | Clamp current health to new max (default)                  |
| `PreservePercentage` | Scale current health proportionally to maintain percentage |

```csharp
using Bezoro.Core.Types;

health.SetMaxHealthTo(50u, MaxValueUpdateMode.ClampCurrent);
// If health was 80/100, it becomes 50/50 (clamped to new max)

health.SetMaxHealthTo(200u, MaxValueUpdateMode.ClampCurrent);
// If health was 50/100, it becomes 50/200

health.SetMaxHealthTo(200u, MaxValueUpdateMode.PreservePercentage);
// If health was 50/100 (50%), it becomes 100/200 (50%)
```

## Damage Integration

DamageSystem uses `IDamageableHealth<T>` to compute applied damage from `EffectiveCurrent`.
For `HealthWithExcess`, effective health includes excess, so damage absorbed by excess counts as applied damage.

## Design Notes

- **Excess-first damage**: `HealthWithExcess.DecreaseHealthBy` drain excess before base health.
- **Restore vs Increase**: Base health exposes `RestoreCurrentHealthBy` only; `HealthWithExcess.IncreaseCurrentHealthBy` overflows into excess while restore never creates excess.
- **Overflow-safe arithmetic**: Additions use `ulong` intermediates and saturate at `uint.MaxValue`.
- **Constructor overflow**: `Health` clamps current to max; `HealthWithExcess` moves overflow into excess (subject to `excessMax`).
- **Excess capacity**: Excess is capped by `excessMax`; the default `excessMax = 0` disables excess capacity unless provided.
