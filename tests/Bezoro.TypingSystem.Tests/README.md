# Bezoro.TypingSystem.Tests

Unit tests for `Bezoro.TypingSystem`, focused on typing-state transitions, validation outcomes, metrics tracking, atomic word consumption, and compatibility behavior.

## Coverage

| Folder | Source Mirror | Description |
| --- | --- | --- |
| `Types` | `src/Bezoro.TypingSystem/Types` | `TypingResult` factories and constructor serialization, state and metrics values, atomic `IWordSource` exhaustion, concurrent `ArrayWordProvider` consumption, mutations and appends after exhaustion, file-adapter append order and failures, temporary-file cleanup, and obsolete compatibility shims |
| `Utilities` | `src/Bezoro.TypingSystem/Utilities` | `TypingValidator` validation behavior and option-driven callbacks |

## Run

```bash
dotnet test tests/Bezoro.TypingSystem.Tests/Bezoro.TypingSystem.Tests.csproj -c Release -f net10.0 --verbosity minimal
```

## Conventions

- Test class naming: `{TypeName}Tests`
- Test method naming: `Method_WhenCondition_ShouldExpectation`
- One behavior per test with explicit Arrange/Act/Assert phases
- Atomic-consumption tests assert that exhaustion returns `false` with empty memory instead of requiring a separate state check
- Concurrent-consumption tests start dedicated workers together and verify each stored word is returned exactly once, including contention for the final word
- Exhaustion tests verify repeated failed reads leave subsequently appended words available
- File-adapter tests use temporary files only when needed and always clean them up in `finally`
- `TypingResult` compatibility tests preserve the legacy constructor's public serialization shape while factories define valid new results
- Compatibility tests cover obsolete interface bridges, the `ArrayWordProvider.AddWordsFromFile` forwarder, and exact migration messages

Tests use the .NET 10 SDK selected by [global.json](../../global.json). They run without Unity or Godot and do not replace validation in engine player builds.

## Workflow regressions

`TypingWorkflowSafetyTests` exercises completion from `TypingState.Initial` through the normal `WithCorrect()` workflow, including 255-character targets. It also covers removing consumed words without losing unread values and constructing providers from single-pass sources without dropping the first word.

Consumer concurrency tests do not authorize concurrent provider mutation. Mutations remain externally synchronized as documented by [the module](../../src/Bezoro.TypingSystem/README.md).
