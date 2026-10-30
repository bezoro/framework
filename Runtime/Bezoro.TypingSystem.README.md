# Bezoro.TypingSystem

Engine-independent word provisioning, character validation, state transitions, callbacks, and performance metrics for typing games and training tools.

## Main types

| Type | Purpose |
| --- | --- |
| `TypingValidator` | Validates one input character against a target span without allocating. |
| `TypingResult` | Describes the expected/input characters, position, length, and validation status. |
| `TypingState` | Tracks immutable position, correct-input, and mistake counts for one target. |
| `TypingValidatorOptions` | Configures case handling, callbacks, and optional metrics collection. |
| `TypingMetrics` | Tracks accuracy, correct characters per minute, mistakes, faults, and elapsed time. |
| `IWordSource` | Atomically consumes words from a typing-session source. |
| `ArrayWordProvider` | Stores words, consumes them once, and provides concrete mutation operations. |
| `WordProviderFileExtensions` | Appends words from a text file to an `ArrayWordProvider`. |

## Quick start

```csharp
using Bezoro.TypingSystem.Types;
using Bezoro.TypingSystem.Utilities;

var target = "hello";
var state = TypingState.Initial;
var metrics = new TypingMetrics();
var options = new TypingValidatorOptions { IgnoreCase = true, Metrics = metrics };

// A game UI would call this once per submitted character.
foreach (char input in "HELLO")
{
    var result = TypingValidator.ValidateInput(target.AsSpan(), state.Position, input, options);
    state = result.IsCorrect ? state.WithCorrect() : state.WithMistake();
}

Console.WriteLine(state.IsComplete((byte)target.Length)); // True
Console.WriteLine($"Accuracy: {metrics.Accuracy:P0}");
```

## API reference

### Validation

`TypingValidator.ValidateInput` returns `Match`, `Completed`, `Mismatch`, `EmptyTarget`, or `PositionOutOfRange`. Targets longer than 255 characters throw `ArgumentOutOfRangeException` because positions are represented as bytes.

Create `TypingResult` values through the public `TypingResult.Match`, `TypingResult.Mismatch`, `TypingResult.Completed`, `TypingResult.EmptyTarget`, and `TypingResult.PositionOutOfRange` factories. The public constructor remains available only as an obsolete compatibility shim for existing callers and serializers during migration.

### State and metrics

Use `TypingState.WithCorrect()` and `TypingState.WithMistake()` to derive the next immutable state. Starting at `TypingState.Initial`, each correct input advances `Position` and `CorrectCount`. `IsComplete(targetLength)` becomes true when all characters have been accepted. Stop submitting input for that target after completion; the next position is beyond its final character. Empty targets are validation faults and do not complete this workflow.

Pass a `TypingMetrics` instance through `TypingValidatorOptions` to record each validation automatically. Metrics and callbacks run synchronously on the validation caller's thread. Callbacks can allocate or throw; the validator does not catch their exceptions. Shared metrics and UI callbacks require external synchronization or dispatch to the engine thread.

### Word providers

Use `IWordSource.TryGetNextWord` as the consumption contract. It atomically reports exhaustion and returns empty memory when no word remains. `ArrayWordProvider` owns the concrete `AddWord`, `AddWords`, `RemoveWord`, `ClearWords`, and `WordCount` mutation and inspection operations.

`ArrayWordProvider` enumerates its constructor input once, supporting sources that can only be consumed once. It rejects empty initial sources. Removing a word uses swapback storage and can change unread order. Removing a consumed word preserves the remaining unread words without replaying consumed ones. `WordCount` counts stored words, including those already consumed; it is not the unread count.

`ArrayWordProvider` supports concurrent consumers: each stored word is reserved by at most one successful call. Exhausted reads do not advance the cursor, so words appended afterward remain available. Mutations, including file loading, must be externally synchronized with all other operations on the provider. The obsolete `HasMoreWords` property is only a snapshot; use `TryGetNextWord` to consume safely.

Load one word per line during caller-controlled setup through the focused file adapter:

```csharp
using Bezoro.TypingSystem.Extensions;
using Bezoro.TypingSystem.Types;

var provider = new ArrayWordProvider(new[] { "first" });
provider.LoadWordsFromFile(path);
```

`IWordProvider`, its split read/mutation members, and `ArrayWordProvider.AddWordsFromFile` remain available as obsolete compatibility shims during the migration window. New callers should consume through `IWordSource`, mutate the concrete provider, and load files through `WordProviderFileExtensions.LoadWordsFromFile`.

## Behavior

- Case-insensitive validation uses invariant Unicode casing.
- Callbacks distinguish matches, completion, mismatches, and validation faults.
- Empty targets and out-of-range positions return fault results instead of throwing.
- File loading is synchronous, reads all lines before appending any words, and is intended for caller-controlled setup paths.

## Runtime notes

- Validation accepts spans so per-keystroke checks do not require string allocation.
- Result and state values are immutable; callers own session-level progression.
- Metrics are optional and kept outside the validator's static core.
- The module targets .NET 10 and .NET Standard 2.1. Unity 6 uses the precompiled .NET Standard DLLs; consume them from Unity scripts using syntax supported by Unity's compiler.

## Build and validation

Use the SDK selected by [global.json](https://github.com/bezoro/framework/blob/2ba319ac003b05275e5e8fe2c435086e8edac345/global.json):

```bash
dotnet build src/Bezoro.TypingSystem/Bezoro.TypingSystem.csproj -c Release
dotnet test tests/Bezoro.TypingSystem.Tests/Bezoro.TypingSystem.Tests.csproj -c Release -f net10.0
```

See [the TypingSystem tests](https://github.com/bezoro/framework/blob/2ba319ac003b05275e5e8fe2c435086e8edac345/tests/Bezoro.TypingSystem.Tests/README.md) for completion, single-pass source, removal, and concurrent-consumer coverage.

