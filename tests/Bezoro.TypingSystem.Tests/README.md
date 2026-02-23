# Bezoro.TypingSystem.Tests
Unit tests for `Bezoro.TypingSystem`, focused on typing-state transitions, validation outcomes, metrics tracking, and word-provider behavior.

## Test Areas
| Folder | Source Mirror | Description |
| --- | --- | --- |
| `Types` | `src/Bezoro.TypingSystem/Types` | `TypingResult`, `TypingState`, `TypingMetrics`, `TypingValidatorOptions`, and `ArrayWordProvider` contracts |
| `Utilities` | `src/Bezoro.TypingSystem/Utilities` | `TypingValidator` validation behavior and option-driven callbacks |

## Quick Start
```bash
dotnet test tests/Bezoro.TypingSystem.Tests/Bezoro.TypingSystem.Tests.csproj
```

## Conventions
- Test class naming: `{TypeName}Tests`
- Test method naming: `Method_WhenCondition_ShouldExpectation`
- One behavior per test with explicit Arrange/Act/Assert phases
- File-system dependent tests (for `AddWordsFromFile`) use temporary files and always clean up
