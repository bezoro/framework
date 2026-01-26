# Damage System

A lightweight, genre-agnostic damage pipeline built around `IDamageable`. It supports simple "deal X damage" calls, multi-type component damage, and an optional rule pipeline for resistances, critical hits, and custom formulas.

## Use Cases

- **Action RPGs**: Physical + elemental damage, crits, resistances
- **Souls-likes**: Flat defenses, true damage, clamped health loss
- **Adventure/Zelda-like**: Simple, reliable "hit for N" damage
- **Monster Hunter-style**: Multi-component hits (raw + elemental)
- **Roguelikes**: Custom rules for scaling, traits, and perks

## Features

- **Straightforward API**: One-liners like `DamageSystem.Apply(target, 25)`
- **Typed damage**: Use `DamageType` identifiers (e.g., `Physical`, `Fire`)
- **Multi-component damage**: Combine damage types in one hit
- **Rule pipeline**: Optional `IDamageRule` stages for advanced logic
- **Configurable rounding/clamping**: Safe defaults with opt-out control
- **Genre-agnostic**: No hardcoded assumptions about combat style

## Quick Start

```csharp
IDamageable target = /* your damageable implementation */;

// Simple hit
DamageSystem.Apply(target, 35);

// Typed hit
DamageSystem.Apply(target, 15f, DamageType.Fire);

// Request with bonuses and flags
var request = new DamageRequest(
	baseAmount: 50f,
	type: DamageType.Physical,
	multiplier: 1.10f,
	flatBonus: 3f,
	flags: DamageFlags.Critical
);

DamageResult result = DamageSystem.Apply(target, request);
```

### Multi-Component Damage

```csharp
var components = new[]
{
	new DamageComponent(DamageType.Physical, 40f),
	new DamageComponent(DamageType.Fire, 10f)
};

var request = DamageRequest.FromComponents(components, multiplier: 1.25f);
DamageSystem.Apply(target, request);
```

## Configuration & Rules

Use a custom resolver when you want resistances, crits, armor rules, or any bespoke logic.

```csharp
var resistances = new DamageResistanceTable();
resistances[DamageType.Fire] = new DamageResistance(multiplier: 0.75f);
resistances[DamageType.Physical] = new DamageResistance(multiplier: 0.90f, flat: -2f);

var resolver = new DamageResolver(new DamageResolverConfig(
	rules: new IDamageRule[]
	{
		new DamageResistanceRule(resistances),
		new CriticalDamageRule(multiplier: 2.0f)
	},
	roundingMode: DamageRoundingMode.Floor,
	minimumAppliedDamage: 0,
	maximumAppliedDamage: 9999,
	clampToCurrentHealth: true
));

DamageSystem.Apply(target, request, resolver);
```

## Damage Participants

`DamageRequest.Source` uses `IDamageSource`, while `DamageRequest.Target` uses `IDamageable` (which extends `IDamageSource`).
This lets sources be traps, projectiles, or abilities that do not have health.

```csharp
public sealed class Trap : IDamageSource
{
	public object? DamageContext => this;
}

public sealed class Enemy : IDamageable
{
	public IHealth Health { get; }
	public object? DamageContext => this;
}

var request = new DamageRequest(
	baseAmount: 25f,
	type: DamageType.Physical,
	source: trap,
	target: enemy
);
```

### Flags

- `DamageFlags.Critical` triggers `CriticalDamageRule` (if installed)
- `DamageFlags.True` can be used to bypass rules that honor it (e.g., `DamageResistanceRule`)

## Order of Operations

1. Build components (from `DamageRequest.Components` or `BaseAmount` + `Type`)
2. Apply rules (`IDamageRule.Apply`) to mutate components and context
3. Sum component amounts
4. Add flat bonuses (`request.FlatBonus` + `context.GlobalFlatBonus`)
5. Apply multipliers (`request.Multiplier` * `context.GlobalMultiplier`)
6. Round and clamp (per `DamageResolverConfig`)
7. Apply to `Target.Health` via `DecreaseCurrentHealthBy`

## API Reference

### DamageSystem

Convenience entry point.

| Member                                           | Description                           |
|--------------------------------------------------|---------------------------------------|
| `Apply(IDamageable, int)`                            | Simple damage, unspecified type       |
| `Apply(IDamageable, float, DamageType)`              | Simple typed damage                   |
| `Apply(IDamageable, DamageRequest)`                  | Apply a request with default resolver |
| `Apply(IDamageable, DamageRequest, IDamageResolver)` | Apply with a custom resolver          |

### DamageRequest

| Member              | Description                                  |
|---------------------|----------------------------------------------|
| `BaseAmount`        | Base damage amount (when no components)      |
| `Type`              | Damage type for base damage                  |
| `Multiplier`        | Multiplier applied after flat bonuses        |
| `FlatBonus`         | Flat bonus added after rules                 |
| `Flags`             | Optional flags (critical, true damage, etc.) |
| `Source` / `Target` | Optional `IDamageSource` / `IDamageable`     |
| `Components`        | Optional list of `DamageComponent`           |

### DamageResult

| Member                         | Description                          |
|--------------------------------|--------------------------------------|
| `HealthBefore` / `HealthAfter` | Health snapshot around the hit       |
| `IntendedDamage`               | Damage after rounding and clamping   |
| `AppliedDamage`                | Actual applied damage (health delta) |
| `RawDamage`                    | Unrounded raw value after rules      |
| `WasCancelled`                 | True if a rule cancelled the hit     |
| `WasFatal`                     | True if health reached 0 or below    |
| `Overkill`                     | Intended damage that did not apply   |

### DamageResolverConfig

| Member                 | Description                           |
|------------------------|---------------------------------------|
| `Rules`                | Ordered list of `IDamageRule`         |
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

- `IDamageRule` lets you inject any custom rule (armor, shields, immunity)
- `DamageResistanceRule` reads adjustments from `IDamageResistanceProvider`
- `DamageResistanceTable` is a simple dictionary-based provider

## Notes

- The resolver assumes `IDamageable.Health` is authoritative for clamping or validation.
- If `ClampToCurrentHealth` is true, intended damage is capped by current health (when current is non-negative).
- Use `DamageContext.Cancel()` inside a rule to nullify a hit.
