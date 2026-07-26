# Bezoro.TypingSystem.Tests

Unit tests for `Bezoro.TypingSystem`, focused on typing-state transitions, validation outcomes, metrics tracking, atomic word consumption, and compatibility behavior.

## Test Areas

| Folder | Source Mirror | Description |
| --- | --- | --- |
| `Types` | `src/Bezoro.TypingSystem/Types` | `TypingResult` factories and constructor serialization, state and metrics values, atomic `IWordSource` exhaustion, `ArrayWordProvider` mutations, file-adapter append order and failures, temporary-file cleanup, and obsolete compatibility shims |
| `Utilities` | `src/Bezoro.TypingSystem/Utilities` | `TypingValidator` validation behavior and option-driven callbacks |

## Quick Start

```bash
dotnet test tests/Bezoro.TypingSystem.Tests/Bezoro.TypingSystem.Tests.csproj
```

## Conventions

- Test class naming: `{TypeName}Tests`
- Test method naming: `Method_WhenCondition_ShouldExpectation`
- One behavior per test with explicit Arrange/Act/Assert phases
- Atomic-consumption tests assert that exhaustion returns `false` with empty memory instead of requiring a separate state check
- File-adapter tests use temporary files only when needed and always clean them up in `finally`
- `TypingResult` compatibility tests preserve the legacy constructor's public serialization shape while factories define valid new results
- Compatibility tests cover obsolete interface bridges, the `ArrayWordProvider.AddWordsFromFile` forwarder, and exact migration messages
