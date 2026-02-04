# Damage System

A lightweight, genre-agnostic damage pipeline built around `IDamageable<THealth>`. It supports simple "deal X damage" calls, multi-type component damage, and an optional rule pipeline for resistances, critical hits, and custom formulas.

## Use Cases

- **Action RPGs**: Physical + elemental damage, crits, resistances
- **Souls-likes**: Flat defenses, true damage, clamped health loss
- **Adventure/Zelda-like**: Simple, reliable "hit for N" damage
- **Monster Hunter-style**: Multi-component hits (raw + elemental)
- **Roguelikes**: Custom rules for scaling, traits, and perks

## Features

- **Straightforward API**: One-liners like `DamageService.Apply(target, 25)`
- **Typed damage**: Use `DamageType` identifiers (e.g., `Physical`, `Fire`)
- **Multi-component damage**: Combine damage types in one hit
- **Rule pipeline**: Optional `IDamageRule<THealth>` stages for advanced logic
- **Configurable rounding/clamping**: Safe defaults with opt-out control
- **Thread-safe updates**: `DamageContext<THealth>` is immutable and `TryUpdateHealth` enables atomic health writes

## Quick Start

```csharp
using Bezoro.GameSystems.DamageSystem.Services;
using Bezoro.GameSystems.DamageSystem.Types;
using Bezoro.GameSystems.HealthSystem.Types;

IDamageable<HealthWithExcess> target = /* your damageable implementation */;

// Simple hit
DamageService.Apply(target, 35);

// Typed hit
DamageService.Apply(target, 15f, DamageType.Fire);

// Request with bonuses and flags
var request = new DamageRequest(
	baseAmount: 50f,
	type: DamageType.Physical,
	multiplier: 1.10f,
	flatBonus: 3f,
	flags: DamageFlags.Critical
);

DamageResult result = DamageService.Apply(target, request);
```

### Multi-Component Damage

```csharp
var components = new[]
{
	new DamageComponent(DamageType.Physical, 40f),
	new DamageComponent(DamageType.Fire, 10f)
};

var request = DamageRequest.FromComponents(components, multiplier: 1.25f);
DamageService.Apply(target, request);
```

## Configuration & Rules

Use a custom resolver when you want resistances, crits, armor rules, or any bespoke logic.

```csharp
using Bezoro.GameSystems.DamageSystem.Abstractions;
using Bezoro.GameSystems.DamageSystem.Resistances;
using Bezoro.GameSystems.DamageSystem.Rules;
using Bezoro.GameSystems.DamageSystem.Services;
using Bezoro.GameSystems.DamageSystem.Types;
using Bezoro.GameSystems.HealthSystem.Types;

var resistances = new DamageResistanceTable();
resistances[DamageType.Fire] = new DamageResistance(multiplier: 0.75f);
resistances[DamageType.Physical] = new DamageResistance(multiplier: 0.90f, flat: -2f);

var resolver = new DamageResolver<HealthWithExcess>(new DamageResolverConfig<HealthWithExcess>(
	rules: new IDamageRule<HealthWithExcess>[]
	{
		new DamageResistanceRule<HealthWithExcess>(resistances),
		new CriticalDamageRule<HealthWithExcess>(multiplier: 2.0f)
	},
	roundingMode: DamageRoundingMode.Floor,
	minimumAppliedDamage: 0,
	maximumAppliedDamage: 9999,
	clampToCurrentHealth: true
));

DamageService.Apply(target, request, resolver);
```

## Damage Participants

`DamageRequest.Source` uses `IDamageSource`. The target is always supplied to the resolver and available via `DamageContext<THealth>`.
Types can implement both when they are also valid damage sources (e.g., enemies, players).

```csharp
using System.Collections.Generic;
using Bezoro.GameSystems.DamageSystem.Abstractions;
using Bezoro.GameSystems.HealthSystem.Types;

public sealed class Trap : IDamageSource
{
	public object? Source => this;
}

public sealed class Enemy : IDamageable<HealthWithExcess>, IDamageSource
{
	private readonly object _gate = new();
	private HealthWithExcess _health = new(max: 100u);

	public HealthWithExcess Health
	{
		get
		{
			lock (_gate)
				return _health;
		}
	}

	public bool TryUpdateHealth(HealthWithExcess expected, HealthWithExcess updated)
	{
		lock (_gate)
		{
			if (!EqualityComparer<HealthWithExcess>.Default.Equals(_health, expected))
				return false;

			_health = updated;
			return true;
		}
	}

	public object? Source => this;
}

var request = new DamageRequest(
	baseAmount: 25f,
	type: DamageType.Physical,
	source: new Trap()
);

DamageService.Apply(new Enemy(), request);
```

### Flags

- `DamageFlags.Critical` triggers `CriticalDamageRule` (if installed)
- `DamageFlags.True` can be used to bypass rules that honor it (e.g., `DamageResistanceRule`)

## Order of Operations

1. Build components (from `DamageRequest.Components` or `BaseAmount` + `Type`)
2. Apply rules (`IDamageRule<THealth>.Apply`) to return an updated context (may replace components or cancel)
3. Sum component amounts
4. Add flat bonuses (`request.FlatBonus` + `context.GlobalFlatBonus`)
5. Apply multipliers (`request.Multiplier` * `context.GlobalMultiplier`)
6. Round and clamp (per `DamageResolverConfig<THealth>`)
7. Atomically apply to target health via `IDamageable<THealth>.TryUpdateHealth`
8. Compute `AppliedDamage` from effective-health delta (includes excess when applicable)

## API Reference

### DamageService

Convenience entry point.

| Member                                                        | Description                           |
|---------------------------------------------------------------|---------------------------------------|
| `Apply<T>(IDamageable<T>, int)`                               | Simple damage, unspecified type       |
| `Apply<T>(IDamageable<T>, float, DamageType)`                 | Simple typed damage                   |
| `Apply<T>(IDamageable<T>, DamageRequest)`                     | Apply a request with default resolver |
| `Apply<T>(IDamageable<T>, DamageRequest, IDamageResolver<T>)` | Apply with a custom resolver          |

### IDamageable<THealth>

| Member                           | Description                                  |
|----------------------------------|----------------------------------------------|
| `THealth Health`                 | Snapshot of current health                   |
| `TryUpdateHealth(expected, next)`| Atomic health update used by the resolver    |

### IDamageableHealth<T>

| Member                 | Description                                               |
|------------------------|-----------------------------------------------------------|
| `uint EffectiveCurrent`| Current health used for damage calculations               |
| `ApplyDamage(uint)`    | Applies damage and returns the updated health             |

### DamageRequest

| Member              | Description                                  |
|---------------------|----------------------------------------------|
| `BaseAmount`        | Base damage amount (when no components)      |
| `Type`              | Damage type for base damage                  |
| `Multiplier`        | Multiplier applied after flat bonuses        |
| `FlatBonus`         | Flat bonus added after rules                 |
| `Flags`             | Optional flags (critical, true damage, etc.) |
| `Source`            | Optional `IDamageSource`                     |
| `Components`        | Optional list of `DamageComponent`           |

### DamageResult

| Member                         | Description                                   |
|--------------------------------|-----------------------------------------------|
| `HealthBefore` / `HealthAfter` | Effective health snapshot around the hit      |
| `IntendedDamage`               | Damage after rounding and clamping            |
| `AppliedDamage`                | Actual applied damage (effective health delta)|
| `RawDamage`                    | Unrounded raw value after rules               |
| `WasCancelled`                 | True if a rule cancelled the hit              |
| `WasFatal`                     | True if effective health reached 0            |
| `Overkill`                     | Intended damage that did not apply            |

### DamageResolverConfig<THealth>

| Member                 | Description                           |
|------------------------|---------------------------------------|
| `Rules`                | Ordered list of `IDamageRule<THealth>`|
| `RoundingMode`         | How raw damage becomes an int         |
| `MinimumAppliedDamage` | Lower clamp after rounding            |
| `MaximumAppliedDamage` | Optional upper clamp                  |
| `ClampToCurrentHealth` | Cap intended damage to current health |

### Damage Types

`DamageType` is a string-based identifier. Use built-ins or define your own:

```csharp
var holy = new DamageType("holy");
```

Built-in types include `Physical`, `Fire`, `Ice`, `Lightning`, `Poison`, `Magic`, `True`, and `Unspecified`.

### Rules & Resistances

- `IDamageRule<THealth>` lets you inject any custom rule (armor, shields, immunity)
- `IDamageRule<THealth>.Apply` returns the updated context (use `AddFlatBonus`, `MultiplyAll`, `Cancel`, or `with`)
- `DamageResistanceRule<THealth>` reads adjustments from `IDamageResistanceProvider`
- `DamageResistanceTable` is a simple dictionary-based provider

## Notes

- The resolver uses `EffectiveCurrent` for clamping and applied-damage calculations.
- If `ClampToCurrentHealth` is true, intended damage is capped by effective current health.
- Use `context = context.Cancel()` inside a rule to nullify a hit.
