# Bezoro.Chess.UCI.Protocol
Low-level UCI transport, parsing, and engine-client orchestration for chess engines.

## Types
| Type                         | Namespace                             | Description                                                                                               |
|------------------------------|---------------------------------------|-----------------------------------------------------------------------------------------------------------|
| `UciEngineClient`            | `Bezoro.Chess.UCI.Protocol.API`       | Main async client for handshake, search orchestration, typed protocol events, and raw engine interaction. |
| `ProcessUciTransport`        | `Bezoro.Chess.UCI.Protocol.API`       | Process-backed stdin/stdout/stderr transport for a UCI engine executable.                                 |
| `SearchParameters`           | `Bezoro.Chess.UCI.Protocol.API.Types` | Standard UCI `go` parameters with explicit validation.                                                    |
| `SearchResult`               | `Bezoro.Chess.UCI.Protocol.API.Types` | Parsed result of a completed search.                                                                      |
| `UciClientOptions`           | `Bezoro.Chess.UCI.Protocol.API.Types` | Client-level timeout and protocol-behavior configuration.                                                 |
| `PositionScore`              | `Bezoro.Chess.UCI.Protocol.API.Types` | Player-relative centipawn or mate score with compact display/sort helpers.                                |
| `PositionAdvantage`          | `Bezoro.Chess.UCI.Protocol.API.Types` | Normalized player-relative advantage summary for lightweight UIs.                                         |
| `MoveClassificationFlags`    | `Bezoro.Chess.UCI.Protocol.API.Types` | Structural and tactical flags such as capture, promotion, check, mate, and stalemate.                    |
| `MoveClassification`         | `Bezoro.Chess.UCI.Protocol.API.Types` | Fully typed move metadata including moving piece, captured piece, promotion piece, and resolved flags.    |
| `MoveEvaluation`             | `Bezoro.Chess.UCI.Protocol.API.Types` | Absolute player-relative score for a legal move candidate's resulting position plus classification data.  |
| `MatchSideControllerKind`    | `Bezoro.Chess.UCI.Protocol.API.Types` | Per-side control mode for playable matches: manual or engine-driven.                                      |
| `PlayableMatchCommand`       | `Bezoro.Chess.UCI.Protocol.API.Types` | Parsed textual command for playable-match workflows such as UCI moves, history, and FEN loading.         |
| `PlayableMatchCommandKind`   | `Bezoro.Chess.UCI.Protocol.API.Types` | Command discriminator for `PlayableMatchCommand`.                                                          |
| `PlayedMove`                 | `Bezoro.Chess.UCI.Protocol.API.Types` | Chronological played-move record including parent/result positions and best-known classification.         |
| `PlayableMatchState`         | `Bezoro.Chess.UCI.Protocol.API.Types` | Current playable-match snapshot including position, legal moves, move classifications, advantage, and history. |
| `EngineMoveResult`           | `Bezoro.Chess.UCI.Protocol.API.Types` | Engine move and search result produced during a playable match turn.                                      |
| `MoveAnalysisResult`         | `Bezoro.Chess.UCI.Protocol.API.Types` | Immutable snapshot of analyzed legal moves for a position.                                                |
| `PositionAnalysisResult`     | `Bezoro.Chess.UCI.Protocol.API.Types` | Shared current-position advantage and legal-move analysis snapshot from the same search flow.             |
| `UciProtocolMessage`         | `Bezoro.Chess.UCI.Protocol.API.Types` | Immutable envelope for parsed protocol output with typed optional payload fields keyed by `Type`.         |
| `UciInfoMessage`             | `Bezoro.Chess.UCI.Protocol.API.Types` | Typed `info ...` payload including score, depth, PV, refutation, and current line data.                   |
| `UciBestMoveMessage`         | `Bezoro.Chess.UCI.Protocol.API.Types` | Typed `bestmove ...` payload.                                                                             |
| `UciIdMessage`               | `Bezoro.Chess.UCI.Protocol.API.Types` | Typed `id name` / `id author` payload.                                                                    |
| `UciOptionMessage`           | `Bezoro.Chess.UCI.Protocol.API.Types` | Typed `option name ...` payload.                                                                          |
| `UciEngineOption`            | `Bezoro.Chess.UCI.Protocol.API.Types` | Parsed engine option metadata in advertised order.                                                        |
| `UciEngineCapabilities`      | `Bezoro.Chess.UCI.Protocol.API.Types` | Observed and probed capability state for standard commands and extensions.                                |
| `UciRegistration`            | `Bezoro.Chess.UCI.Protocol.API.Types` | Payload for the standard `register` command.                                                              |
| `EngineActivity`             | `Bezoro.Chess.UCI.Protocol.API.Types` | Coarse engine activity state.                                                                             |
| `TransportStatus`            | `Bezoro.Chess.UCI.Protocol.API.Types` | Transport lifecycle state.                                                                                |
| `UciPositionAnalysisCoordinator` | `Bezoro.Chess.UCI.Protocol.API`   | FIFO full-strength position-analysis queue with completed-result caching by position key.                 |
| `UciPlayableMatchSession`    | `Bezoro.Chess.UCI.Protocol.API`       | Stateful playable-match coordinator with manual/manual, manual/engine, or engine/engine side control. |

## Quick Start
```csharp
using Bezoro.Chess.UCI.Protocol.API;
using Bezoro.Chess.UCI.Protocol.API.Types;

var options = new UciClientOptions
{
    UciHandshakeTimeout = TimeSpan.FromSeconds(5),
    ReadyTimeout = TimeSpan.FromSeconds(10),
    DefaultSearchTimeout = TimeSpan.FromSeconds(30)
};

await using var client = new UciEngineClient(enginePath, options: options);
await client.StartAsync(cancellationToken);

await client.SetPositionAsync(Fen.Default, null, cancellationToken);

var result = await client.GoAsync(
    new SearchParameters { Depth = 12 },
    cancellationToken);

Console.WriteLine($"Best move: {result.BestMove}");
Console.WriteLine($"Ponder:    {result.PonderMove}");
Console.WriteLine($"Eval cp:   {result.BestCpScore}");
```

## Typed Protocol Stream
```csharp
using Bezoro.Chess.UCI.Protocol.API;
using Bezoro.Chess.UCI.Protocol.API.Types;

await using var client = new UciEngineClient(enginePath);

client.InfoReceived += message =>
{
    var payload = message.Payload;
    if (payload.Depth is not uint depth || payload.PrincipalVariation is not { } pv)
        return;

    Console.WriteLine($"depth {depth}: {pv.RawPv}");
};

client.BestMoveMessageReceived += message =>
    Console.WriteLine($"bestmove {message.BestMove} ponder {message.PonderMove}");

client.ProtocolMessageReceived += message =>
    Console.WriteLine($"{message.Type}: {message.RawLine}");

client.StderrReceived += line =>
    Console.Error.WriteLine($"stderr: {line}");

await client.StartAsync(cancellationToken);
await client.SetPositionAsync(Fen.Default, null, cancellationToken);
await client.GoFireAndForgetAsync(new SearchParameters { Infinite = true }, cancellationToken);

await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
await client.StopSearchAsync(cancellationToken);
await client.IsReadyAsync(cancellationToken);
```

Compatibility events such as `InfoPvReceived`, `BestMoveReceived`, and `LineReceived` remain available for existing callers, but the typed events are the primary surface.

Collection-bearing protocol snapshots use immutable storage:
- `SearchParameters.SearchMoves`
- `SearchResult.PrincipalVariations`
- `PrincipalVariation.Moves`
- `UciInfoPayload.Refutation` / `CurrentLine`
- `UciEngineOption.Variables`
- `UciEngineClient.AvailableOptions`

## Option Discovery And Validation
```csharp
using Bezoro.Chess.UCI.Protocol.API;
using Bezoro.Chess.UCI.Protocol.API.Types;

await using var client = new UciEngineClient(enginePath);
await client.StartAsync(cancellationToken);

Console.WriteLine($"{client.EngineInfo.Name} by {client.EngineInfo.Author}");

if (client.TryGetOption("Threads", out var threads))
{
    Console.WriteLine($"Threads default: {threads.DefaultValue}");
    await client.SetOptionAsync(threads.Name, "4", cancellationToken);
}

foreach (var option in client.AvailableOptions)
{
    Console.WriteLine($"{option.Name} [{option.Type}] default={option.DefaultValue}");
}

if (client.TryGetStrengthLimitRange(out int minElo, out int maxElo))
{
    int targetElo = Math.Clamp(1500, minElo, maxElo);
    await client.SetStrengthLimitAsync(targetElo, cancellationToken);
}
```

`SearchParameters` now requires at least one explicit search limit or mode such as `Depth`, `Nodes`, `MoveTimeMs`, `WhiteTimeMs`/`BlackTimeMs`, `Mate`, `Infinite`, or `Ponder`. Empty `go` requests are rejected instead of silently defaulting to a fallback search.

`SetOptionAsync` rejects blank option names with `ArgumentException` instead of silently no-oping.

`TryGetStrengthLimitRange` and `SetStrengthLimitAsync` are small ergonomic helpers over the standard `UCI_LimitStrength` / `UCI_Elo` option pair. They keep raw option-name plumbing out of application code while still respecting the engine's advertised metadata.

## Engine-Specific Escape Hatches
```csharp
using Bezoro.Chess.UCI.Protocol.API;
using Bezoro.Chess.UCI.Protocol.API.Types;

await using var client = new UciEngineClient(enginePath);
await client.StartAsync(cancellationToken);
await client.SetPositionAsync(Fen.Default, ["e2e4", "e7e5"], cancellationToken);

if (client.Capabilities.SupportsCoordinatorExtensions)
{
    Fen? currentFen = await client.TryGetFenViaDisplayBoardAsync(cancellationToken);
    var legalMoves = await client.GetLegalMovesViaPerftAsync(cancellationToken);
}
```

`TryGetFenViaDisplayBoardAsync` and `GetLegalMovesViaPerftAsync` are deliberate non-standard helpers. They remain available because higher layers in this repo depend on them, but they should be treated as engine-specific extensions rather than standard UCI behavior.

## Move Classification Helpers
```csharp
using Bezoro.Chess.UCI.Protocol.API.Common.Extensions;
using Bezoro.Chess.UCI.Protocol.API.Types;

Fen fen = Fen.Parse("7k/5Q2/7K/8/8/8/8/8 w - - 0 1")!.Value;

MoveClassification structuralOnly = fen.ClassifyMove("f7g7");
MoveClassification full = fen.ClassifyMoveFully("f7g7");

Console.WriteLine(structuralOnly.IsResolved); // false
Console.WriteLine(full.IsCheck);              // true
Console.WriteLine(full.IsMate);               // true
```

`ClassifyMove` and `ClassifyMoves` are the zero-search structural fast path. `ClassifyMoveFully` and `ClassifyMovesFully` resolve check, mate, and stalemate locally from FEN plus UCI move notation, without engine round-trips.

Debug-display helpers are also available for simple text UIs:
```csharp
using Bezoro.Chess.UCI.Protocol.API.Common.Extensions;

string evaluationText = evaluation.ToDebugDisplayString();
string historySuffix = playedMove.Classification.ToDebugSuffix();
```

`PlayedMoveHistoryExtensions.ToDisplayLines` now includes those classification suffixes automatically when the played moves carry them.

## Playable Match Command Parsing
```csharp
using Bezoro.Chess.UCI.Protocol.API;
using Bezoro.Chess.UCI.Protocol.API.Types;

PlayableMatchCommand command = PlayableMatchCommandParser.Parse(
    "loadfen rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1 moves e2e4 e7e5");

if (command.Kind == PlayableMatchCommandKind.LoadFen)
{
    Fen fen = command.Fen!.Value;
    var moves = command.Moves;
}
```

This parser is intended for terminal apps or debug tooling built on top of `UciPlayableMatchSession`. It recognizes UCI moves, `moves`, `history`, `undo`, `quit`, and `loadfen <fen> [moves ...]`.

## Analysis Helpers
```csharp
using Bezoro.Chess.UCI.Protocol.API;
using Bezoro.Chess.UCI.Protocol.API.Common.Extensions;
using Bezoro.Chess.UCI.Protocol.API.Types;

await using var client = new UciEngineClient(enginePath);
await client.StartAsync(cancellationToken);
await client.SetPositionAsync(Fen.Default, ["e2e4", "e7e5"], cancellationToken);

Fen currentFen = (await client.TryGetFenViaDisplayBoardAsync(cancellationToken))!.Value;
var legalMoves = (await client.GetLegalMovesViaPerftAsync(cancellationToken)).NormalizeUciMoves();
var advantage = await client.EvaluateAdvantageAsync(currentFen.ActiveColor, 'w', ct: cancellationToken);
var moveEvaluations = await client.AnalyzeLegalMovesAsync(
    currentFen.ActiveColor,
    playerColor: 'w',
    legalMoves,
    ct: cancellationToken);

var positionAnalysis = await client.AnalyzePositionAsync(
    currentFen.ActiveColor,
    playerColor: 'w',
    legalMoves,
    ct: cancellationToken);
```

`EvaluateAdvantageAsync` and `AnalyzeLegalMovesAsync` package the score-normalization, MultiPV probing, fallback single-move evaluation, and display-friendly absolute move-score logic that previously only lived in the console sample. Their centipawn values are player-relative current evaluations; they are not offset against a start-position calibration.

`AnalyzePositionAsync` and `UciPositionAnalysisCoordinator` are intended for non-blocking UIs that want the current advantage bar and legal-move list to come from the same full-strength analysis stream. The coordinator processes queued positions in FIFO order, caches each completed result by position key, and never skips earlier queued positions in favor of newer ones.

## Playable Match Workflow
```csharp
using Bezoro.Chess.UCI.Protocol.API;
using Bezoro.Chess.UCI.Protocol.API.Types;

await using var playingClient = new UciEngineClient(enginePath);
await using var analysisClient = new UciEngineClient(enginePath);
await using var moveListClient = new UciEngineClient(enginePath);

await Task.WhenAll(
    playingClient.StartAsync(cancellationToken),
    analysisClient.StartAsync(cancellationToken),
    moveListClient.StartAsync(cancellationToken));

var session = new UciPlayableMatchSession(
    playingClient,
    analysisClient,
    moveListClient,
    perspectiveColor: 'w',
    whiteController: MatchSideControllerKind.Manual,
    blackController: MatchSideControllerKind.Engine);

await session.StartNewGameAsync(cancellationToken);

PlayableMatchState state = await session.RefreshAsync(cancellationToken);
var openingAnalysis = await session.GetLegalMoveAnalysisAsync(cancellationToken);
var legalMoveClassifications = state.LegalMoveClassifications;

session.ApplyMove("e2e4");
state = await session.RefreshAsync(cancellationToken);

if (session.GetController(state.Fen.ActiveColor) == MatchSideControllerKind.Engine)
{
    EngineMoveResult engineMove = await session.PlayControlledMoveAsync(cancellationToken);
    state = await session.RefreshAsync(cancellationToken);
}

await session.WaitForCurrentMoveClassificationsAsync(cancellationToken);
string[] historyLines = session.GetMoveHistoryDisplayLines();
PositionAdvantage liveAdvantage = session.ResolveCurrentAdvantage();

if (session.CanUndoMoves())
{
    session.UndoMoves();
    state = await session.RefreshAsync(cancellationToken);
}
```

`UciPlayableMatchSession` keeps the sample's reusable match orchestration in the library: position refresh, legal-move loading, local move-type resolution, move-history tracking, controller-driven automatic engine turns, and current advantage resolution from the same full-strength move evaluations used for move lists and debugging history. Structural move types are available immediately; check, mate, and stalemate are resolved by the background classifier without blocking gameplay.

## Text Formatting Helpers
```csharp
using Bezoro.Chess.UCI.Protocol.API.Common.Extensions;

string[] boardLines = fen.ToDisplayLines(playerColor: 'w', legalMoveCount: legalMoves.Length);
string[] advantageLines = advantage.ToDisplayBarLines();
string engineLine = result.ToDisplayString();
string playerEngineLine = result.ToPlayerDisplayString(sideToMove: 'b', playerColor: 'w');
```

These helpers are intentionally lightweight. They are suitable for samples, diagnostics, or simple terminal UIs without forcing consumers into a richer board-rendering abstraction.

## API Reference
### `UciPlayableMatchSession`
| Member                           | Description                                                                 |
|----------------------------------|-----------------------------------------------------------------------------|
| `StartNewGameAsync(ct)`          | Clears local state and sends `ucinewgame` to the playing, snapshot, and move-list clients. |
| `RefreshAsync(ct)`               | Reloads the current FEN, legal moves, move history, and live non-blocking advantage. |
| `GetLegalMoveAnalysisAsync(ct)`  | Awaits the full-strength move analysis for the current position.           |
| `GetCurrentLegalMoveClassifications()` | Returns the latest cached move-type map for the current position.    |
| `WaitForCurrentMoveClassificationsAsync(ct)` | Awaits completion of background check/mate/stalemate resolution for the current position. |
| `TryGetLegalMoveClassification(move, out classification)` | Reads a cached move classification for the current position. |
| `ApplyMove(move)`                | Validates and applies a move for the current manually controlled side.     |
| `ApplyHumanMove(move)`           | Compatibility alias for `ApplyMove` in human-versus-engine flows.          |
| `PlayControlledMoveAsync(ct)`    | Plays the current side's move when that side is engine-controlled.         |
| `PlayEngineMoveAsync(ct)`        | Compatibility alias for `PlayControlledMoveAsync` in human-versus-engine flows. |
| `CanUndoMoves(count)`            | Reports whether the requested number of played moves can be undone.        |
| `UndoMoves(count)`               | Rewinds played moves while preserving reachable cached analysis and classifications. |
| `TryGetPlayedMoveScore(move, out score)` | Resolves a played move back to its parent-position high-quality score. |
| `TryGetPlayedMoveClassification(move, out classification)` | Resolves the latest known classification for a played move. |
| `TryGetPositionAnalysis(key, out analysis)` | Reads a completed cached position analysis when available.           |
| `ResolveCurrentAdvantage()`      | Resolves the best completed current advantage without blocking gameplay.    |
| `GetMoveHistoryDisplayLines()`   | Builds simple debugging lines for played-move history.                     |
| `CancelAnalysis()`               | Cancels in-flight full-strength analysis.                                  |
| `PerspectiveColor`               | Side used for board-orientation and player-relative evaluation helpers.    |
| `WhiteController` / `BlackController` | Per-side controller kinds (`Manual` or `Engine`).                    |
| `GetController(side)`            | Returns the configured controller for `w` or `b`.                          |
| `PlayerColor` / `EngineColor`    | Compatibility helpers for single-human/single-engine sessions only.        |
| `PlayedMoves` / `MoveHistory`    | Current raw played moves and structured played-move history.               |
| `CurrentState`                   | Latest refreshed match snapshot.                                           |

### `UciEngineClient`
| Member                                       | Description                                                                      |
|----------------------------------------------|----------------------------------------------------------------------------------|
| `StartAsync(ct)`                             | Starts the engine process, read loop, and performs UCI handshake.                |
| `StopAsync(ct)`                              | Stops the client and underlying transport gracefully.                            |
| `UciInitAsync(ct)`                           | Sends `uci` and waits for `uciok` and `readyok`. Usually called by `StartAsync`. |
| `IsReadyAsync(ct)`                           | Sends `isready` and waits for `readyok`.                                         |
| `UciNewGameAsync(ct)`                        | Sends `ucinewgame` and waits for readiness.                                      |
| `SetOptionAsync(name, value, ct)`            | Sends `setoption`; completes after `readyok`; throws on blank names.             |
| `SetDebugAsync(enabled, ct)`                 | Sends `debug on` or `debug off`.                                                 |
| `RegisterAsync(registration, ct)`            | Sends the standard `register` command.                                           |
| `PonderHitAsync(ct)`                         | Sends `ponderhit`.                                                               |
| `SetPositionAsync(fen, moves, ct)`           | Sends `position startpos ...` or `position fen ...`.                             |
| `GoAsync(parameters, ct)`                    | Runs a bounded search and returns a parsed `SearchResult`.                       |
| `GoFireAndForgetAsync(parameters, ct)`       | Starts a search without waiting for `bestmove`.                                  |
| `StopSearchAsync(ct)`                        | Sends `stop`.                                                                    |
| `TryGetFenViaDisplayBoardAsync(ct)`          | Requests current FEN using the non-standard `d` command.                         |
| `GetLegalMovesViaPerftAsync(ct)`             | Requests legal moves using the non-standard `go perft 1` listing.                |
| `BuildGoCommand(parameters)`                 | Utility for building a validated raw UCI `go` command string.                    |
| `TryGetOption(name, out option)`             | Looks up an advertised option with case-insensitive matching.                    |
| `TryGetStrengthLimitRange(out min, out max)` | Reads the advertised Elo range for engines that support `UCI_LimitStrength`.     |
| `SetStrengthLimitAsync(elo, ct)`             | Enables `UCI_LimitStrength` and sets `UCI_Elo` with range-aware validation.      |
| `EngineInfo`                                 | Engine name and author parsed during handshake.                                  |
| `AvailableOptions`                           | Options advertised during handshake, preserved in advertised order.              |
| `Capabilities`                               | Standard and extension capability state discovered so far.                       |
| `Options`                                    | Client-level timeout configuration.                                              |
| `InfoReceived`                               | Event for parsed `UciInfoMessage` output.                                        |
| `BestMoveMessageReceived`                    | Event for parsed `UciBestMoveMessage` output.                                    |
| `ProtocolMessageReceived`                    | Event for all parsed protocol messages.                                          |
| `RawLineReceived`                            | Event for every raw stdout line.                                                 |
| `StderrReceived`                             | Event for redirected stderr lines.                                               |
| `ActivityChanged`                            | Event for `Idle`/`Searching`/`Pondering` transitions.                            |

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
| Property                                | Meaning                                                                |
|-----------------------------------------|------------------------------------------------------------------------|
| `Depth`                                 | Search to a fixed depth. Must be greater than zero when set.           |
| `Nodes`                                 | Search a fixed number of nodes. Must be greater than zero when set.    |
| `MoveTimeMs`                            | Search for a fixed amount of time. Must be greater than zero when set. |
| `WhiteTimeMs` / `BlackTimeMs`           | Remaining clock time. Must be greater than zero when set.              |
| `WhiteIncrementMs` / `BlackIncrementMs` | Clock increments. Must not be negative.                                |
| `MovesToGo`                             | Number of moves until the next time control. Must not be negative.     |
| `Mate`                                  | Search for a mate in N. Must be greater than zero when set.            |
| `SearchMoves`                           | Restrict search to specific moves. Stored as an immutable snapshot.    |
| `Infinite`                              | Search until explicitly stopped.                                       |
| `Ponder`                                | Start a ponder search.                                                 |

### `UciProtocolMessageType`
| Value            | Meaning                                |
|------------------|----------------------------------------|
| `Id`             | Parsed `id name` / `id author` output. |
| `Option`         | Parsed `option name ...` output.       |
| `UciOk`          | Parsed `uciok` output.                 |
| `ReadyOk`        | Parsed `readyok` output.               |
| `Info`           | Parsed `info ...` output.              |
| `BestMove`       | Parsed `bestmove ...` output.          |
| `CopyProtection` | Parsed `copyprotection ...` output.    |
| `Registration`   | Parsed `registration ...` output.      |

## Sample
See `samples/Bezoro.Chess.UCI.Protocol.ConsoleDemo` for an interactive playable console sample. It prompts for engine Elo and player color, renders the board from engine-reported FEN, validates human moves against the engine's legal-move listing, and lets the engine answer with timed searches. The `moves` command, `history` command, and current cp bar all resolve from the same full-strength move evaluations, with positions analyzed in move order instead of skipping ahead to the latest position. Move types such as capture, en passant, promotion, castling, check, mate, and stalemate are resolved through the protocol library and carried through both legal-move lists and played-move history. The sample also supports `loadfen <fen> [moves ...]` for targeted debugging of specific positions and edge cases.

## Design Notes
- This project owns transport lifecycle, line dispatch, command serialization, handshake parsing, typed protocol messages, and safe async protocol behavior.
- The protocol layer stays engine-agnostic for standard UCI behavior.
- Extension probing exists here only as an explicit low-level escape hatch; higher-level policy about requiring those extensions belongs in `Bezoro.Chess.UCI`.
- `SetOptionAsync` waits for `readyok`, which makes option updates safe to compose in application code.
