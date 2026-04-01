# Bezoro.Chess.UCI.Tests
Unit and integration tests for `Bezoro.Chess.UCI`.

## Test Areas
| Folder | Source Mirror | Description |
| --- | --- | --- |
| `API` | `src/Bezoro.Chess.UCI/API` | Coordinator workflows, event/state behavior, and capability failure paths. |
| `API/Common` | `src/Bezoro.Chess.UCI/API/Common` | Chess piece enum and extension behavior used by classification. |
| `API/Types` | `src/Bezoro.Chess.UCI/API/Types` | Board-state and move-analysis semantics. |
| `Domain/Engines` | `src/Bezoro.Chess.UCI/Domain/Engines` | Quick, ponder, and move-classification orchestration. |
| `TestHelpers` | Test-only | Shared builders, fixtures, and shared Stockfish resource paths. |

## Quick Start
```bash
dotnet test tests/Bezoro.Chess.UCI.Tests/Bezoro.Chess.UCI.Tests.csproj
```

## Useful Commands
```bash
dotnet test tests/Bezoro.Chess.UCI.Tests/Bezoro.Chess.UCI.Tests.csproj --filter "FullyQualifiedName~UciCoordinator"
dotnet test tests/Bezoro.Chess.UCI.Tests/Bezoro.Chess.UCI.Tests.csproj --filter "FullyQualifiedName~MoveClassificationEngine"
dotnet test tests/Bezoro.Chess.UCI.Tests/Bezoro.Chess.UCI.Tests.csproj --filter "Category=Integration"
```

## What These Tests Guarantee
- `UciCoordinator` keeps quick, ponder, and classifier engines synchronized.
- Move classification produces stable chess/UI semantics such as capture, castling, promotion, check, and mate flags.
- Coordinator startup fails clearly when required engine extensions are unavailable.
- State streaming, granular game-engine events, and `SynchronizationContext` dispatch remain safe for app or UI integration.
