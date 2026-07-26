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
| `IWordSource` | Atomically consumes words from a typing-session source. |
| `ArrayWordProvider` | Stores words in insertion order and provides concrete mutation operations. |
| `WordProviderFileExtensions` | Appends words from a text file to an `ArrayWordProvider`. |

## Quick Start

```csharp
using Bezoro.TypingSystem.Abstractions;
using Bezoro.TypingSystem.Types;
using Bezoro.TypingSystem.Utilities;

IWordSource source = new ArrayWordProvider(["hello", "world"]);
var metrics = new TypingMetrics();
var options = new TypingValidatorOptions
{
	IgnoreCase = true,
	Metrics = metrics
};

while (source.TryGetNextWord(out var word))
{
	var result = TypingValidator.ValidateInput(word.Span, position: 0, inputChar: 'H', options);

	if (result.IsCorrect)
		Console.WriteLine($"Accuracy: {metrics.Accuracy:P0}");
}
```

## API Reference

### Validation

`TypingValidator.ValidateInput` returns `Match`, `Completed`, `Mismatch`, `EmptyTarget`, or `PositionOutOfRange`. Targets longer than 255 characters throw `ArgumentOutOfRangeException` because positions are represented as bytes.

Create `TypingResult` values through the public `TypingResult.Match`, `TypingResult.Mismatch`, `TypingResult.Completed`, `TypingResult.EmptyTarget`, and `TypingResult.PositionOutOfRange` factories. The public constructor remains available only as an obsolete compatibility shim for existing callers and serializers during migration.

### State And Metrics

Use `TypingState.WithCorrect()` and `TypingState.WithMistake()` to derive the next immutable state. Pass a `TypingMetrics` instance through `TypingValidatorOptions` to record each validation automatically.

### Word Providers

Use `IWordSource.TryGetNextWord` as the consumption contract. It atomically reports exhaustion and returns empty memory when no word remains. `ArrayWordProvider` owns the concrete `AddWord`, `AddWords`, `RemoveWord`, `ClearWords`, and `WordCount` mutation and inspection operations.

Load one word per line during caller-controlled setup through the focused file adapter:

```csharp
using Bezoro.TypingSystem.Extensions;
using Bezoro.TypingSystem.Types;

var provider = new ArrayWordProvider(["first"]);
provider.LoadWordsFromFile(path);
```

`IWordProvider`, its split read/mutation members, and `ArrayWordProvider.AddWordsFromFile` remain available as obsolete compatibility shims during the migration window. New callers should consume through `IWordSource`, mutate the concrete provider, and load files through `WordProviderFileExtensions.LoadWordsFromFile`.

## Feature Notes

- Case-insensitive validation uses invariant Unicode casing.
- Callbacks distinguish matches, completion, mismatches, and validation faults.
- Empty targets and out-of-range positions return fault results instead of throwing.
- File loading is synchronous, streams lines from the file, and is intended for caller-controlled setup paths.

## Design Notes

- Validation accepts spans so per-keystroke checks do not require string allocation.
- Result and state values are immutable; callers own session-level progression.
- Metrics are optional and kept outside the validator's static core.
- The module targets .NET 9 and .NET Standard 2.1 for Unity-compatible consumers.
