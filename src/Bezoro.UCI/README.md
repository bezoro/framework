# Bezoro.UCI
UCI transport, engine-client, and coordination utilities for talking to chess engines such as Stockfish.

## Types
| Type                  | Description                                                                                   |
|-----------------------|-----------------------------------------------------------------------------------------------|
| `UciCoordinator`      | High-level orchestration across quick-eval, ponder, and move-classification engine instances. |
| `UciEngineInfo`       | Engine name/author metadata captured from the UCI handshake.                                  |
| `UciEngineOption`     | Typed option metadata parsed from `option name ...` handshake lines.                          |
| `UciEngineCapabilities` | Capability state for standard commands and Bezoro.UCI extension requirements.               |
| `ProcessUciTransport` | Process-backed stdin/stdout/stderr transport for UCI engines.                                 |
| `UciEngineClient`     | Low-level engine command and output-dispatch client built on a transport.                     |

## Quick Start
```csharp
await using var coordinator = await UciCoordinator.CreateAsync(enginePath, ct: cancellationToken);
var info = coordinator.EngineInfo;
var capabilities = coordinator.Capabilities;

await coordinator.UpdatePositionAsync(Fen.Default, null, cancellationToken);

var best = await coordinator.SearchAsync(new SearchParameters { Depth = 12 }, cancellationToken);
```

## Design Notes
- Engine output dispatch is isolated from user event handlers so subscriber exceptions do not break protocol processing.
- `UciEngineClient` now captures `id ...` and `option ...` handshake lines and exposes typed metadata/capability state to higher layers.
- Standard UCI commands `debug`, `register`, `setoption`, `position`, and `ponderhit` are built explicitly by the command layer; `setoption` waits for `readyok` before completing.
- `UciCoordinator.SetOptionAsync(...)` applies the option to all internal engine instances to keep behavior aligned.
- `UciCoordinator` exposes `EngineInfo`, `AvailableOptions`, and `Capabilities` from the quick engine handshake.
- `ProcessUciTransport` keeps stdout/stderr open through graceful shutdown so final engine output can drain before teardown completes.
- `GetFenViaDAsync()` and `GetLegalMovesViaGoPerft1Async()` still rely on non-standard engine support (`d` output and `go perft 1` move listing). The coordinator now probes those extensions during startup and fails early with a clear error when they are unavailable.
