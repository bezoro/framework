# Bezoro.TypingSystem

Engine-independent word provisioning, character validation, state transitions, callbacks, and performance metrics for typing games and training tools.

## Key Types

| Type | Purpose |
| --- | --- |
| `TypingValidator` | Validates one input character against a target span without allocating. |
| `TypingResult` | Describes the expected/input characters, position, length, and validation status. |
| `TypingState` | Tracks immutable position, correct-input, and mistake counts for one target. |
| `TypingValidatorOptions` | Configures case handling, callbacks, and optional metrics collection. |
| `TypingMetrics` | Tracks accuracy, correct characters per minute, mistakes, faults, and elapsed time. |
| `IWordProvider` / `ArrayWordProvider` | Supplies and mutates a sequence of words. |

## Quick Start

```csharp
using Bezoro.TypingSystem.Types;
using Bezoro.TypingSystem.Utilities;

var metrics = new TypingMetrics();
var options = new TypingValidatorOptions
{
	IgnoreCase = true,
	Metrics = metrics
};

ReadOnlySpan<char> target = "hello";
var result = TypingValidator.ValidateInput(target, position: 0, inputChar: 'H', options);

if (result.IsCorrect)
	Console.WriteLine($"Accuracy: {metrics.Accuracy:P0}");
```

## API Reference

### Validation

`TypingValidator.ValidateInput` returns `Match`, `Completed`, `Mismatch`, `EmptyTarget`, or `PositionOutOfRange`. Targets longer than 255 characters throw `ArgumentOutOfRangeException` because positions are represented as bytes.

### State And Metrics

Use `TypingState.WithCorrect()` and `TypingState.WithMistake()` to derive the next immutable state. Pass a `TypingMetrics` instance through `TypingValidatorOptions` to record each validation automatically.

### Word Providers

`IWordProvider` supports adding, removing, clearing, and reading words. `ArrayWordProvider` consumes its words in insertion order and throws `InvalidOperationException` when exhausted.

## Feature Notes

- Case-insensitive validation uses invariant Unicode casing.
- Callbacks distinguish matches, completion, mismatches, and validation faults.
- Empty targets and out-of-range positions return fault results instead of throwing.
- Word loading from files is synchronous and intended for caller-controlled setup paths.

## Design Notes

- Validation accepts spans so per-keystroke checks do not require string allocation.
- Result and state values are immutable; callers own session-level progression.
- Metrics are optional and kept outside the validator's static core.
- The module targets .NET 9 and .NET Standard 2.1 for Unity-compatible consumers.
