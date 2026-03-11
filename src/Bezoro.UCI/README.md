# Bezoro.UCI
UCI transport, engine-client, and coordination utilities for talking to chess engines such as Stockfish.

## Types
| Type                  | Description                                                                                   |
|-----------------------|-----------------------------------------------------------------------------------------------|
| `UciCoordinator`      | High-level orchestration across quick-eval, ponder, and move-classification engine instances. |
| `ProcessUciTransport` | Process-backed stdin/stdout/stderr transport for UCI engines.                                 |
| `UciEngineClient`     | Low-level engine command and output-dispatch client built on a transport.                     |

## Quick Start
```csharp
await using var coordinator = await UciCoordinator.CreateAsync(enginePath, ct: cancellationToken);
await coordinator.UpdatePositionAsync(Fen.Default, null, cancellationToken);

var best = await coordinator.SearchAsync(new SearchParameters { Depth = 12 }, cancellationToken);
```

## Design Notes
- Engine output dispatch is isolated from user event handlers so subscriber exceptions do not break protocol processing.
- `UciCoordinator.SetOptionAsync(...)` applies the option to all internal engine instances to keep behavior aligned.
- `ProcessUciTransport` keeps stdout/stderr open through graceful shutdown so final engine output can drain before teardown completes.
- `GetFenViaDAsync()` and `GetLegalMovesViaGoPerft1Async()` currently rely on Stockfish-style output. Generic-UCI capability detection and fallbacks are still pending.
