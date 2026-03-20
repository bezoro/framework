# Bezoro.Chess.UCI
High-level chess-analysis orchestration built on `Bezoro.Chess.UCI.Protocol`.

## Types
| Type                             | Namespace                           | Description                                                                                    |
|----------------------------------|-------------------------------------|------------------------------------------------------------------------------------------------|
| `UciCoordinator`                 | `Bezoro.Chess.UCI.API`              | Public facade for quick search, pondering, move classification, and synchronized engine state. |
| `UciCoordinatorOptions`          | `Bezoro.Chess.UCI.API.Types`        | Tuning for ponder threads, MultiPV, and classification depth.                                  |
| `UciState`                       | `Bezoro.Chess.UCI.API.Types`        | Immutable snapshot of board, search, and move-classification state.                            |
| `Move`                           | `Bezoro.Chess.UCI.API.Types`        | Classified move plus semantic UI-facing analysis.                                              |
| `MoveAnalysis`                   | `Bezoro.Chess.UCI.API.Types`        | Flags such as capture, castling, check, mate, promotion, and stalemate.                        |
| `BoardState`                     | `Bezoro.Chess.UCI.API.Types`        | Board model derived from FEN for move classification logic.                                    |
| `ParsedMove`                     | `Bezoro.Chess.UCI.API.Types`        | Parsed move notation with source/target squares and promotion info.                            |
| `MoveScore`                      | `Bezoro.Chess.UCI.API.Types`        | Evaluation score for a move in centipawns or mate distance.                                    |
| `Piece`, `Position`, `Promotion` | `Bezoro.Chess.UCI.API.Types`        | Chess-semantic types used by classification and UI layers.                                     |
| `PieceColor`, `PieceType`        | `Bezoro.Chess.UCI.API.Common.Enums` | Chess enums used by move and board analysis.                                                   |

## Quick Start
```csharp
using Bezoro.Chess.UCI.API;
using Bezoro.Chess.UCI.Protocol.API.Types;

await using var coordinator = await UciCoordinator.CreateAsync(
    enginePath,
    ct: cancellationToken);

await coordinator.UpdatePositionAsync(Fen.Default, null, cancellationToken);

var result = await coordinator.SearchAsync(
    new SearchParameters { Depth = 12 },
    cancellationToken);

var move = await coordinator.ClassifyMoveAsync(result.BestMove, cancellationToken);

Console.WriteLine($"Best move: {move.Notation}");
Console.WriteLine($"Capture:   {move.Analysis.IsCapture}");
Console.WriteLine($"Castling:  {move.Analysis.IsCastling}");
```

## Ideal Usage
### Use the coordinator as a game-facing facade
```csharp
using Bezoro.Chess.UCI.API;
using Bezoro.Chess.UCI.Protocol.API.Types;

await using var coordinator = await UciCoordinator.CreateAsync(enginePath, ct: cancellationToken);

await coordinator.ResetAsync(cancellationToken);

coordinator.StateChanged += state =>
{
    Console.WriteLine($"Moves: {state.TotalLegalMoves}, classified: {state.ClassifiedMovesCount}");
};

var engineResult = await coordinator.SearchAsync(
    new SearchParameters { Depth = 10 },
    cancellationToken);

await coordinator.MakeMoveAsync(engineResult.BestMove, cancellationToken);
```

### Drive UI feedback from classified moves
```csharp
using Bezoro.Chess.UCI.API;
using Bezoro.Chess.UCI.Protocol.API.Types;

await using var coordinator = await UciCoordinator.CreateAsync(enginePath, ct: cancellationToken);
await coordinator.UpdatePositionAsync(Fen.Default, null, cancellationToken);

await foreach (var move in coordinator.StreamClassifiedMovesAsync(cancellationToken))
{
    string color = move.Analysis switch
    {
        { IsMate: true } => "magenta",
        { IsCapture: true } => "red",
        { IsCastling: true } => "yellow",
        { IsPromotion: true } => "cyan",
        _ => "white"
    };

    Console.WriteLine($"{move.Notation} => {color}");
}
```

### Keep a Unity or UI thread synchronized
```csharp
using Bezoro.Chess.UCI.API;
using Bezoro.Chess.UCI.Protocol.API.Types;

SynchronizationContext uiContext = SynchronizationContext.Current!;

await using var coordinator = await UciCoordinator.CreateAsync(
    enginePath,
    syncContext: uiContext,
    ct: cancellationToken);

coordinator.Ready += () => Console.WriteLine("Engine ready");
coordinator.StateChanged += state =>
{
    // Safe to bind UI here because callbacks are marshaled to uiContext.
    Console.WriteLine(state.CurrentFen.Raw);
};

await coordinator.UpdatePositionAsync(Fen.Default, null, cancellationToken);
await coordinator.StartSearchAsync(cancellationToken);
```

### Configure coordinator-level engine behavior
```csharp
using Bezoro.Chess.UCI.API;
using Bezoro.Chess.UCI.API.Types;
using Bezoro.Chess.UCI.Protocol.API.Types;

var options = new UciCoordinatorOptions(
    PonderThreads: 2,
    MultiPv: 3,
    ClassificationDepth: 8);

await using var coordinator = await UciCoordinator.CreateAsync(
    enginePath,
    options,
    ct: cancellationToken);

await coordinator.SetOptionAsync("Hash", "256", cancellationToken);
await coordinator.SetDebugAsync(false, cancellationToken);
```

## API Reference
### `UciCoordinator`
| Member                                                            | Description                                                         |
|-------------------------------------------------------------------|---------------------------------------------------------------------|
| `CreateAsync(enginePath, options, syncContext, ct)`               | Constructs and starts a ready-to-use coordinator.                   |
| `StartAsync(ct)` / `StopAsync(ct)`                                | Starts or stops all internal engine instances.                      |
| `ResetAsync(ct)` / `NewGameAsync(ct)`                             | Resets position state or starts a fresh engine game context.        |
| `UpdatePositionAsync(fen, playedMoves, ct)`                       | Synchronizes quick, ponder, and classifier engines to a position.   |
| `SetPositionAsync(fenString, ct)`                                 | Convenience wrapper that parses a FEN string.                       |
| `StartSearchAsync(ct)` / `StartSearchAsync(fen, playedMoves, ct)` | Starts infinite pondering search.                                   |
| `StopSearchAsync(ct)`                                             | Stops the current ponder search.                                    |
| `SearchAsync(parameters, ct)`                                     | Runs a bounded quick search and returns `SearchResult`.             |
| `ClassifyMoveAsync(move, ct)`                                     | Returns semantic move analysis for one legal move.                  |
| `StreamClassifiedMovesAsync(ct)`                                  | Streams classifications as they complete.                           |
| `WaitForClassificationAsync(ct)`                                  | Waits for all legal moves to be classified.                         |
| `MakeMoveAsync(move, ct)`                                         | Applies a legal move and refreshes state.                           |
| `UndoAsync(count, ct)`                                            | Rewinds one or more played moves.                                   |
| `SetOptionAsync(name, value, ct)`                                 | Applies a UCI option to all internal engine instances.              |
| `SetDebugAsync(enabled, ct)`                                      | Broadcasts `debug on/off` to all internal engines.                  |
| `RegisterAsync(registration, ct)`                                 | Broadcasts the standard `register` command to all internal engines. |
| `GetCurrentFenAsync(ct)`                                          | Returns the engine-reported current FEN if available.               |
| `State`                                                           | Current immutable snapshot.                                         |
| `EngineInfo` / `AvailableOptions` / `Capabilities`                | Handshake metadata surfaced from the quick engine.                  |
| `StateChanged`, `Ready`, `Stopped`, `Error`                       | High-level events for application integration.                      |

### `UciState`
| Property                                              | Meaning                                                        |
|-------------------------------------------------------|----------------------------------------------------------------|
| `BaseFen`                                             | Starting position before `PlayedMoves` are applied.            |
| `CurrentFen`                                          | Effective current position after applied moves.                |
| `PlayedMoves`                                         | Move history from `BaseFen`.                                   |
| `LegalMoves`                                          | Currently legal moves in raw UCI notation.                     |
| `ClassifiedMoves`                                     | Completed move classifications keyed by notation.              |
| `BestMove` / `PonderMove`                             | Best and ponder moves from the current search, when available. |
| `Evaluation`                                          | Latest principal variation from the ponder engine.             |
| `IsSearching`                                         | Whether a ponder search is active.                             |
| `IsCheck`, `IsCheckmate`, `IsStalemate`, `IsGameOver` | Derived convenience flags.                                     |
| `ClassificationProgress`                              | Fraction of legal moves already classified.                    |

### `Move` and `MoveAnalysis`
| Member                                             | Meaning                                                 |
|----------------------------------------------------|---------------------------------------------------------|
| `Move.Notation`                                    | Raw UCI notation like `e2e4`.                           |
| `Move.From` / `Move.To`                            | Source and target squares.                              |
| `Move.Piece` / `Move.MovingSide`                   | Moving piece metadata.                                  |
| `Move.Analysis.IsCapture`                          | True for captures, including en passant.                |
| `Move.Analysis.IsCastling`                         | True for castling moves.                                |
| `Move.Analysis.IsPromotion`                        | True for promotion moves.                               |
| `Move.Analysis.IsCheck` / `IsMate` / `IsStalemate` | Tactical end-state flags derived during classification. |
| `Move.Analysis.Score`                              | Engine score associated with the move.                  |

### `UciCoordinatorOptions`
| Property              | Meaning                                       |
|-----------------------|-----------------------------------------------|
| `PonderThreads`       | Thread count applied to the ponder engine.    |
| `MultiPv`             | MultiPV value applied to the ponder engine.   |
| `ClassificationDepth` | Search depth used during move classification. |
| `Default`             | Safe default configuration.                   |

## Design Notes
- This project hides transport/protocol complexity behind one game-facing facade.
- `UciCoordinator` owns quick evaluation, pondering, background move classification, and optional `SynchronizationContext` dispatch for UI threads.
- The coordinator currently depends on engine-specific `d` and `go perft 1` support to derive current FEN and legal moves. Those requirements are probed at startup and exposed through `Capabilities`.
- Protocol types such as `Fen`, `SearchParameters`, and `SearchResult` come from `Bezoro.Chess.UCI.Protocol`; this project uses them rather than redefining them.
- Search and metadata snapshots exposed by the protocol layer are immutable, so coordinator state can safely retain and rebroadcast them across threads.
