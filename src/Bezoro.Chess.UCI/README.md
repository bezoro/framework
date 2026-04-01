# Bezoro.Chess.UCI
High-level chess-analysis orchestration built on `Bezoro.Chess.UCI.Protocol`.

## Types
| Type                             | Namespace                           | Description                                                                                    |
|----------------------------------|-------------------------------------|------------------------------------------------------------------------------------------------|
| `UciCoordinator`                 | `Bezoro.Chess.UCI.API`              | Game-engine-facing facade for synchronized position state, search updates, move classification, and UI events. |
| `UciCoordinatorOptions`          | `Bezoro.Chess.UCI.API.Types`        | Tuning for ponder threads, MultiPV, and classification depth.                                  |
| `UciState`                       | `Bezoro.Chess.UCI.API.Types`        | Immutable snapshot of board, search, and move-classification state.                            |
| `Move`                           | `Bezoro.Chess.UCI.API.Types`        | Classified move plus semantic UI-facing analysis.                                              |
| `MoveAnalysis`                   | `Bezoro.Chess.UCI.API.Types`        | Flags such as capture, castling, check, mate, promotion, and stalemate.                        |
| `BoardState`                     | `Bezoro.Chess.UCI.API.Types`        | Board model derived from FEN for move classification logic.                                    |
| `ParsedMove`                     | `Bezoro.Chess.UCI.API.Types`        | Parsed move notation with source/target squares and promotion info.                            |
| `MoveScore`                      | `Bezoro.Chess.UCI.API.Types`        | Evaluation score for a move in centipawns or mate distance.                                    |
| `Piece`, `Position`, `Promotion` | `Bezoro.Chess.UCI.API.Types`        | Chess-semantic types used by classification and UI layers.                                     |
| `GameMoveEvent`                  | `Bezoro.Chess.UCI.API.Types`        | Rich canonical move payload for UI-facing gameplay events.                                     |
| `PromotionRequiredEvent`         | `Bezoro.Chess.UCI.API.Types`        | Pending-promotion payload for request/response UI flows.                                       |
| `PromotionChosenEvent`           | `Bezoro.Chess.UCI.API.Types`        | Promotion-resolution payload emitted before the move is applied.                               |
| `MoveUndoneEvent`                | `Bezoro.Chess.UCI.API.Types`        | Undo payload carrying removed moves and the resulting position.                                |
| `TurnChangedEvent`               | `Bezoro.Chess.UCI.API.Types`        | Active-side transition payload for board and HUD updates.                                      |
| `PositionLoadedEvent`            | `Bezoro.Chess.UCI.API.Types`        | Explicit load payload for save/load, replay, or editor workflows.                              |
| `PieceColor`, `PieceType`        | `Bezoro.Chess.UCI.API.Common.Enums` | Chess enums used by move and board analysis.                                                   |
| `GameMoveActor`, `GameMoveKindFlags` | `Bezoro.Chess.UCI.API.Common.Enums` | UI-facing enums describing move initiators and structural move kinds.                        |

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

### Keep a Unity, Godot, or UI thread synchronized
```csharp
using Bezoro.Chess.UCI.API;
using Bezoro.Chess.UCI.API.Common.Enums;
using Bezoro.Chess.UCI.Protocol.API.Types;

SynchronizationContext uiContext = SynchronizationContext.Current!;

await using var coordinator = await UciCoordinator.CreateAsync(
    enginePath,
    syncContext: uiContext,
    ct: cancellationToken);

coordinator.Ready += () => Console.WriteLine("Engine ready");
coordinator.PositionChanged += state =>
{
    // Safe to bind UI here because callbacks are marshaled to uiContext.
    Console.WriteLine(state.CurrentFen.Raw);
};
coordinator.MoveMade += move => Console.WriteLine($"{move.Actor}: {move.Notation} [{move.KindFlags}]");
coordinator.PromotionRequired += request => ShowPromotionButtons(request.AllowedPromotionPieces);
coordinator.MoveClassificationUpdated += move => Console.WriteLine($"Highlight {move.Notation}");
coordinator.EvaluationUpdated += pv => Console.WriteLine(pv.Raw);
coordinator.EngineThinkingStarted += _ => ShowThinking(true);
coordinator.EngineThinkingStopped += _ => ShowThinking(false);

await coordinator.UpdatePositionAsync(Fen.Default, null, cancellationToken);
await coordinator.StartSearchAsync(cancellationToken);
```

### Drive a promotion request/response flow
```csharp
using Bezoro.Chess.UCI.API;
using Bezoro.Chess.UCI.API.Common.Enums;
using Bezoro.Chess.UCI.Protocol.API.Types;

await using var coordinator = await UciCoordinator.CreateAsync(enginePath, ct: cancellationToken);
await coordinator.UpdatePositionAsync(Fen.Parse("1r5k/P7/8/8/8/8/8/K7 w - - 0 1")!.Value, null, cancellationToken);

PromotionRequiredEvent pending = default;
coordinator.PromotionRequired += request => pending = request;

await coordinator.MakeMoveAsync("a7a8", cancellationToken);
await coordinator.ChoosePromotionAsync(pending.PendingPromotionId, PieceType.Queen, cancellationToken);
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
| `MakeMoveAsync(move, ct)` / `MakeMoveAsync(move, actor, ct)`      | Applies a legal move and refreshes state, optionally tagging the actor. |
| `ChoosePromotionAsync(id, pieceType, ct)`                         | Resolves a pending promotion request and applies the completed move. |
| `UndoAsync(count, ct)`                                            | Rewinds one or more played moves.                                   |
| `SetOptionAsync(name, value, ct)`                                 | Applies a UCI option to all internal engine instances.              |
| `SetDebugAsync(enabled, ct)`                                      | Broadcasts `debug on/off` to all internal engines.                  |
| `RegisterAsync(registration, ct)`                                 | Broadcasts the standard `register` command to all internal engines. |
| `GetCurrentFenAsync(ct)`                                          | Returns the engine-reported current FEN if available.               |
| `State`                                                           | Current immutable snapshot.                                         |
| `EngineInfo` / `AvailableOptions` / `Capabilities`                | Handshake metadata surfaced from the quick engine.                  |
| `GameStarted`                                                     | Raised when a fresh gameplay session starts.                        |
| `MoveMade`                                                        | Canonical rich move event for applied moves.                        |
| `CaptureMade` / `CastlingMade` / `EnPassantMade`                  | Convenience move projections for common UI hooks.                   |
| `PromotionRequired` / `PromotionChosen`                           | Promotion request/response events for UI-driven choice flows.       |
| `Check` / `Checkmate` / `Stalemated`                              | Tactical end-state events for applied moves.                        |
| `MoveUndone`                                                      | Raised when moves are undone.                                       |
| `IllegalMoveRejected`                                             | Raised before an illegal move call throws.                          |
| `TurnChanged`                                                     | Raised when the active side changes.                                |
| `PositionLoaded` / `PositionChanged`                              | Raised for explicit loads and visible board snapshot updates.       |
| `LegalMovesUpdated`                                               | Raised when the visible legal move set changes.                     |
| `SearchStateChanged`                                              | Raised when the coordinator enters or leaves searching state.       |
| `EngineThinkingStarted` / `EngineThinkingStopped`                 | Raised for UI-facing engine-think lifecycle transitions.            |
| `EvaluationChanged` / `EvaluationUpdated`                         | Raised for new principal variations from the ponder engine.         |
| `BestMoveChanged`                                                 | Raised when the current best/ponder move pair changes.              |
| `MoveClassified` / `MoveClassificationUpdated`                    | Raised for each newly classified legal move.                        |
| `ClassificationCompleted`                                         | Raised when the current position's move classification is complete. |
| `GameOver`                                                        | Raised when the current position has no legal moves.                |
| `StateChanged`, `Ready`, `Stopped`, `Error`, `EngineError`        | Coarser lifecycle and integration events.                           |

### `GameMoveEvent`
| Property | Meaning |
| --- | --- |
| `GameId` / `MoveId` / `Ply` | Stable gameplay identifiers for event-driven UI logic. |
| `Actor` | Who initiated the move, such as `Human` or `Engine`. |
| `Notation`, `From`, `To` | Raw UCI move text plus origin and destination squares. |
| `KindFlags` | Structural move kinds such as capture, en passant, castling, promotion, and double pawn push. |
| `MovingPiece`, `CapturedPiece`, `PromotionPiece` | Fully typed piece payloads for board animation and VFX hooks. |
| `SecondaryPieceMove` | Auxiliary piece movement such as the rook leg of castling. |
| `PreviousFen`, `ResultingFen` | Exact before/after board states. |
| `IsCheck`, `IsCheckmate`, `IsStalemate` | Tactical end-state flags after the move is applied. |
| `Evaluation` | Optional engine evaluation for the resulting position when available. |

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
- `UciCoordinator` owns quick evaluation, pondering, background move classification, and optional `SynchronizationContext` dispatch for single-threaded engines and UI threads.
- The coordinator exposes one canonical rich `MoveMade` payload plus convenience events so a game engine can react to board snapshots, search updates, move semantics, undo, promotion, and terminal states without reverse-engineering chess rules from raw UCI strings.
- The coordinator currently depends on engine-specific `d` and `go perft 1` support to derive current FEN and legal moves. Those requirements are probed at startup and exposed through `Capabilities`.
- Protocol types such as `Fen`, `SearchParameters`, and `SearchResult` come from `Bezoro.Chess.UCI.Protocol`; this project uses them rather than redefining them.
- Search and metadata snapshots exposed by the protocol layer are immutable, so coordinator state can safely retain and rebroadcast them across threads.
