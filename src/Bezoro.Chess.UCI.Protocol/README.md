# Bezoro.Chess.UCI.Protocol
Low-level UCI transport and engine-client primitives for talking to chess engines safely.

## Types
| Type                         | Namespace                                    | Description                                                                   |
|------------------------------|----------------------------------------------|-------------------------------------------------------------------------------|
| `UciEngineClient`            | `Bezoro.Chess.UCI.Protocol.API`              | Main async client for handshake, search, options, and raw engine interaction. |
| `ProcessUciTransport`        | `Bezoro.Chess.UCI.Protocol.API`              | Process-backed stdin/stdout/stderr transport for a UCI engine executable.     |
| `ProcessUciTransportOptions` | `Bezoro.Chess.UCI.Protocol.API.Types`        | Buffering, teardown, encoding, and validation options for process transport.  |
| `IUciTransport`              | `Bezoro.Chess.UCI.Protocol.API.Abstractions` | Transport abstraction used by the client.                                     |
| `IUciLineSource`             | `Bezoro.Chess.UCI.Protocol.API.Abstractions` | Read-only line subscription surface for observing engine output.              |
| `Fen`                        | `Bezoro.Chess.UCI.Protocol.API.Types`        | Parsed FEN value used by position and state APIs.                             |
| `SearchParameters`           | `Bezoro.Chess.UCI.Protocol.API.Types`        | Standard UCI `go` parameters.                                                 |
| `SearchResult`               | `Bezoro.Chess.UCI.Protocol.API.Types`        | Parsed result of a completed search.                                          |
| `PrincipalVariation`         | `Bezoro.Chess.UCI.Protocol.API.Types`        | Parsed `info ... pv ...` line.                                                |
| `UciEngineInfo`              | `Bezoro.Chess.UCI.Protocol.API.Types`        | Engine `id name` and `id author` metadata.                                    |
| `UciEngineOption`            | `Bezoro.Chess.UCI.Protocol.API.Types`        | Typed option metadata parsed from `option name ...` lines.                    |
| `UciEngineCapabilities`      | `Bezoro.Chess.UCI.Protocol.API.Types`        | Capability state for standard commands and detected extensions.               |
| `UciRegistration`            | `Bezoro.Chess.UCI.Protocol.API.Types`        | Payload for the standard `register` command.                                  |
| `EngineActivity`             | `Bezoro.Chess.UCI.Protocol.API.Types`        | Coarse engine activity state.                                                 |
| `TransportStatus`            | `Bezoro.Chess.UCI.Protocol.API.Types`        | Transport lifecycle state.                                                    |

## Quick Start
```csharp
using Bezoro.Chess.UCI.Protocol.API;
using Bezoro.Chess.UCI.Protocol.API.Types;

await using var client = new UciEngineClient(enginePath);
await client.StartAsync(cancellationToken);

await client.SetPositionAsync(Fen.Default, null, cancellationToken);

var result = await client.GoAsync(
    new SearchParameters { Depth = 12 },
    cancellationToken);

Console.WriteLine($"Best move: {result.BestMove}");
Console.WriteLine($"Ponder:    {result.PonderMove}");
Console.WriteLine($"Eval cp:   {result.BestPv?.ScoreCp}");
```

## Ideal Usage
### Inspect engine metadata and options
```csharp
using Bezoro.Chess.UCI.Protocol.API;

await using var client = new UciEngineClient(enginePath);
await client.StartAsync(cancellationToken);

Console.WriteLine($"{client.EngineInfo.Name} by {client.EngineInfo.Author}");

foreach (var option in client.AvailableOptions)
{
    Console.WriteLine($"{option.Name} [{option.Type}] default={option.DefaultValue}");
}
```

### Configure the engine and run a bounded search
```csharp
using Bezoro.Chess.UCI.Protocol.API;
using Bezoro.Chess.UCI.Protocol.API.Types;

await using var client = new UciEngineClient(enginePath);
await client.StartAsync(cancellationToken);

await client.SetOptionAsync("Threads", "4", cancellationToken);
await client.SetOptionAsync("Hash", "256", cancellationToken);

var fen = Fen.Parse("r1bqkbnr/pppp1ppp/2n5/4p3/4P3/5N2/PPPP1PPP/RNBQKB1R w KQkq - 2 3")!.Value;
await client.SetPositionAsync(fen, null, cancellationToken);

var result = await client.GoAsync(
    new SearchParameters
    {
        Depth = 15,
        SearchMoves = ["f1b5", "d2d4", "c2c3"]
    },
    cancellationToken);
```

### Observe live search output
```csharp
using Bezoro.Chess.UCI.Protocol.API;
using Bezoro.Chess.UCI.Protocol.API.Types;

await using var client = new UciEngineClient(enginePath);
client.InfoPvReceived += pv =>
{
    Console.WriteLine($"depth {pv.Depth}: {pv.RawPv}");
};

client.BestMoveReceived += (best, ponder) =>
{
    Console.WriteLine($"bestmove {best} ponder {ponder}");
};

await client.StartAsync(cancellationToken);
await client.SetPositionAsync(Fen.Default, null, cancellationToken);
await client.GoFireAndForgetAsync(new SearchParameters { Infinite = true }, cancellationToken);

await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
await client.StopSearchAsync(cancellationToken);
await client.IsReadyAsync(cancellationToken);
```

### Use engine-specific escape hatches deliberately
```csharp
using Bezoro.Chess.UCI.Protocol.API;
using Bezoro.Chess.UCI.Protocol.API.Types;

await using var client = new UciEngineClient(enginePath);
await client.StartAsync(cancellationToken);
await client.SetPositionAsync(Fen.Default, ["e2e4", "e7e5"], cancellationToken);

Fen? currentFen = await client.GetFenViaDAsync(cancellationToken);
IReadOnlyCollection<string> legalMoves = await client.GetLegalMovesViaGoPerft1Async(cancellationToken);
```

Those two helpers rely on non-standard engine behavior. They are useful for Stockfish-style engines, but they are not guaranteed by the UCI spec.

## API Reference
### `UciEngineClient`
| Member                                 | Description                                                                      |
|----------------------------------------|----------------------------------------------------------------------------------|
| `StartAsync(ct)`                       | Starts the engine process, read loop, and performs UCI handshake.                |
| `StopAsync(ct)`                        | Stops the client and underlying transport gracefully.                            |
| `UciInitAsync(ct)`                     | Sends `uci` and waits for `uciok` and `readyok`. Usually called by `StartAsync`. |
| `IsReadyAsync(ct)`                     | Sends `isready` and waits for `readyok`.                                         |
| `UciNewGameAsync(ct)`                  | Sends `ucinewgame` and waits for readiness.                                      |
| `SetOptionAsync(name, value, ct)`      | Sends `setoption`; completes after `readyok`.                                    |
| `SetDebugAsync(enabled, ct)`           | Sends `debug on` or `debug off`.                                                 |
| `RegisterAsync(registration, ct)`      | Sends the standard `register` command.                                           |
| `PonderHitAsync(ct)`                   | Sends `ponderhit`.                                                               |
| `SetPositionAsync(fen, moves, ct)`     | Sends `position startpos ...` or `position fen ...`.                             |
| `GoAsync(parameters, ct)`              | Runs a bounded search and returns a parsed `SearchResult`.                       |
| `GoFireAndForgetAsync(parameters, ct)` | Starts a search without waiting for `bestmove`.                                  |
| `StopSearchAsync(ct)`                  | Sends `stop`.                                                                    |
| `GetFenViaDAsync(ct)`                  | Requests current FEN using the non-standard `d` command.                         |
| `GetLegalMovesViaGoPerft1Async(ct)`    | Requests legal moves using the non-standard `go perft 1` listing.                |
| `BuildGoCommand(parameters)`           | Utility for building a raw UCI `go` command string.                              |
| `IsUciMoveString(value)`               | Validates raw UCI move notation like `e2e4` or `a7a8q`.                          |
| `EngineInfo`                           | Engine name/author parsed during handshake.                                      |
| `AvailableOptions`                     | Options advertised during handshake.                                             |
| `Capabilities`                         | Standard and extension capability state discovered so far.                       |
| `InfoPvReceived`                       | Event for parsed PV lines during search.                                         |
| `BestMoveReceived`                     | Event for `bestmove` output.                                                     |
| `LineReceived`                         | Event for raw output lines.                                                      |
| `ActivityChanged`                      | Event for `Idle`/`Searching`/`Pondering` transitions.                            |

### `ProcessUciTransport`
| Member                                              | Description                                        |
|-----------------------------------------------------|----------------------------------------------------|
| `ReadLinesAsync(ct)`                                | Async line stream from engine stdout.              |
| `WriteLineAsync(line, ct)`                          | Writes a command line to stdin.                    |
| `TryWriteLineAsync(line, timeout, ct)`              | Timeout-bounded write attempt.                     |
| `StartAsync(ct)`                                    | Launches the engine process and background loops.  |
| `StopAsync(ct)`                                     | Stops loops and tears down the process gracefully. |
| `Status`                                            | Current lifecycle state.                           |
| `IsStarted`                                         | Whether the transport has been started.            |
| `IsHealthy`                                         | Best-effort health check for process and loops.    |
| `LinesRead` / `LinesWritten` / `BackpressureEvents` | Transport counters useful during diagnostics.      |

### `SearchParameters`
| Property                                | Meaning                                      |
|-----------------------------------------|----------------------------------------------|
| `Depth`                                 | Search to a fixed depth.                     |
| `Nodes`                                 | Search a fixed number of nodes.              |
| `MoveTimeMs`                            | Search for a fixed amount of time.           |
| `WhiteTimeMs` / `BlackTimeMs`           | Remaining clock time.                        |
| `WhiteIncrementMs` / `BlackIncrementMs` | Clock increments.                            |
| `MovesToGo`                             | Number of moves until the next time control. |
| `Mate`                                  | Search for a mate in N.                      |
| `SearchMoves`                           | Restrict search to specific moves.           |
| `Infinite`                              | Search until explicitly stopped.             |
| `Ponder`                                | Start a ponder search.                       |

### `UciEngineCapabilities`
| Property                        | Meaning                                                         |
|---------------------------------|-----------------------------------------------------------------|
| `DebugCommand`                  | Whether `debug on/off` is supported.                            |
| `RegisterCommand`               | Whether `register` is supported.                                |
| `PonderHit`                     | Whether `ponderhit` is supported.                               |
| `DisplayBoardFen`               | Whether `d`-based FEN retrieval works.                          |
| `PerftMoveListing`              | Whether `go perft 1` move listing works.                        |
| `SupportsCoordinatorExtensions` | Convenience flag for the higher-level coordinator requirements. |

## Design Notes
- This project owns transport lifecycle, line dispatch, command serialization, handshake parsing, and safe async protocol behavior.
- The protocol layer is engine-agnostic for standard UCI behavior.
- Extension probing exists here only as low-level escape-hatch functionality; higher-level policy about requiring those extensions belongs in `Bezoro.Chess.UCI`.
- `SetOptionAsync` waits for `readyok`, which makes option updates safe to compose in application code.
