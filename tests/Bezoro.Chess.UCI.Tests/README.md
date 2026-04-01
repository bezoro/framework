# Bezoro.Chess.UCI.Tests
Unit and integration tests for `Bezoro.Chess.UCI`.

## Test Areas
| Folder | Source Mirror | Description |
| --- | --- | --- |
| `API` | `src/Bezoro.Chess.UCI/API` | Game-engine session workflows, event/state behavior, and capability failure paths. |
| `API/Common` | `src/Bezoro.Chess.UCI/API/Common` | Chess piece enum and extension behavior used by classification. |
| `API/Types` | `src/Bezoro.Chess.UCI/API/Types` | Board-state and move-analysis semantics. |
| `TestHelpers` | Test-only | Shared builders, fixtures, and shared Stockfish resource paths. |

## Quick Start
```bash
dotnet test tests/Bezoro.Chess.UCI.Tests/Bezoro.Chess.UCI.Tests.csproj
```

## Useful Commands
```bash
dotnet test tests/Bezoro.Chess.UCI.Tests/Bezoro.Chess.UCI.Tests.csproj --filter "FullyQualifiedName~UciGameEngineSession"
dotnet test tests/Bezoro.Chess.UCI.Tests/Bezoro.Chess.UCI.Tests.csproj --filter "FullyQualifiedName~UciGameEngineSessionGameEventModel"
dotnet test tests/Bezoro.Chess.UCI.Tests/Bezoro.Chess.UCI.Tests.csproj --filter "Category=Integration"
```

## What These Tests Guarantee
- `UciGameEngineSession` exposes the preferred game-engine-facing API and event model.
- `UciGameEngineSession` mirrors the protocol layer's controller-neutral side model for manual/manual, manual/engine, and engine/engine local play.
- `UciGameEngineSession` keeps protocol-backed snapshot, ponder, and classification flows synchronized.
- Move classification produces stable chess/UI semantics such as capture, castling, promotion, check, and mate flags.
- Result adjudication, draw-offer flow, claimable draws, and protocol-backed chess clocks remain visible through the game-engine facade.
- Session startup fails clearly when required engine extensions are unavailable.
- State streaming, rich move payloads, promotion request/response events, undo events, and `SynchronizationContext` dispatch remain safe for app or UI integration.
