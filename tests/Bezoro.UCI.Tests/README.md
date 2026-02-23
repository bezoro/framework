# Bezoro.UCI.Tests
Unit and integration tests for `Bezoro.UCI`, covering coordinator workflows, engine-client commands, transport lifecycle/IO behavior, and UCI domain types.

## Test Areas
| Folder                  | Source Mirror                          | Description                                                                                   |
|-------------------------|----------------------------------------|-----------------------------------------------------------------------------------------------|
| `API`                   | `src/Bezoro.UCI/API`                   | `UciCoordinator` orchestration, API types, and API extension behavior                         |
| `Domain`                | `src/Bezoro.UCI/Domain`                | `ProcessUciTransport` lifecycle/read/write/event flows and `UciEngineClient` command behavior |
| `Domain/Engines`        | `src/Bezoro.UCI/Domain/Engines`        | Integration coverage for `MoveClassificationEngine`, `PonderEngine`, and `QuickInfoEngine`    |
| `Domain/Common/Helpers` | `src/Bezoro.UCI/Domain/Common/Helpers` | `UciHelper` parsing and validation behavior                                                   |
| `TestHelpers`           | Test-only                              | Shared builders, constants, and utility helpers                                               |
| `TestResources/Engine`  | Test-only                              | Embedded Stockfish binary/resources for integration scenarios                                 |

## Quick Start
```bash
dotnet test tests/Bezoro.UCI.Tests/Bezoro.UCI.Tests.csproj
```

## Conventions
- Test class naming: `{TypeName}Tests`
- Test method naming: `Method_WhenCondition_ShouldExpectation`
- Non-unit tests are explicitly marked with `[Trait("Category", "Integration")]`
- Integration tests isolate engine process interactions and keep assertions deterministic with bounded timeouts
