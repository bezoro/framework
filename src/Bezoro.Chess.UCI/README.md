# Bezoro.Chess.UCI
High-level game-engine event layer built on `Bezoro.Chess.UCI.Protocol`.

## Types
| Type                             | Namespace                           | Description                                                                                    |
|----------------------------------|-------------------------------------|------------------------------------------------------------------------------------------------|
| `UciGameEngineSession`           | `Bezoro.Chess.UCI.API`              | Preferred game-engine-facing facade for synchronized position state, search updates, move classification, and UI events. |
| `UciCoordinatorOptions`          | `Bezoro.Chess.UCI.API.Types`        | Tuning for ponder threads, MultiPV, classification depth, engine move time, draw policy, and clocks. |
| `UciState`                       | `Bezoro.Chess.UCI.API.Types`        | Immutable snapshot of board, search, move-classification, result, draw-offer, and clock state. |
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
| `UciGameEngineSessionEvent` / `UciGameEngineSessionEventKind` | `Bezoro.Chess.UCI.API.Types` | Compact typed event stream payload and kind enum for session integrations. |
| `PieceColor`, `PieceType`        | `Bezoro.Chess.UCI.API.Common.Enums` | Chess enums used by move and board analysis.                                                   |
| `GameMoveActor`, `GameMoveKindFlags` | `Bezoro.Chess.UCI.API.Common.Enums` | UI-facing enums describing move initiators and structural move kinds.                        |

## Quick Start
```csharp
using Bezoro.Chess.UCI.API;
using Bezoro.Chess.UCI.Protocol.API.Types;

await using var session = await UciGameEngineSession.CreateAsync(
    enginePath,
    perspectiveColor: 'b',
    whiteController: MatchSideControllerKind.Engine,
    blackController: MatchSideControllerKind.Manual,
    ct: cancellationToken);

await session.UpdatePositionAsync(Fen.Default, null, cancellationToken);

var result = await session.SearchAsync(
    new SearchParameters { Depth = 12 },
    cancellationToken);

var move = await session.ClassifyMoveAsync(result.BestMove, cancellationToken);

Console.WriteLine($"Best move: {move.Notation}");
Console.WriteLine($"Capture:   {move.Analysis.IsCapture}");
Console.WriteLine($"Castling:  {move.Analysis.IsCastling}");
```

## Ideal Usage
### Use the game-engine session facade
```csharp
using Bezoro.Chess.UCI.API;
using Bezoro.Chess.UCI.Protocol.API.Types;

await using var session = await UciGameEngineSession.CreateAsync(enginePath, ct: cancellationToken);

await session.ResetAsync(cancellationToken);

session.StateChanged += state =>
{
    Console.WriteLine($"Moves: {state.TotalLegalMoves}, classified: {state.ClassifiedMovesCount}");
};

var engineResult = await session.SearchAsync(
    new SearchParameters { Depth = 10 },
    cancellationToken);

await session.MakeMoveAsync(engineResult.BestMove, cancellationToken);
```

### Drive UI feedback from classified moves
```csharp
using Bezoro.Chess.UCI.API;
using Bezoro.Chess.UCI.Protocol.API.Types;

await using var session = await UciGameEngineSession.CreateAsync(enginePath, ct: cancellationToken);
await session.UpdatePositionAsync(Fen.Default, null, cancellationToken);

await foreach (var move in session.StreamClassifiedMovesAsync(cancellationToken))
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

await using var session = await UciGameEngineSession.CreateAsync(
    enginePath,
    syncContext: uiContext,
    perspectiveColor: 'w',
    whiteController: MatchSideControllerKind.Manual,
    blackController: MatchSideControllerKind.Engine,
    ct: cancellationToken);

session.Ready += () => Console.WriteLine("Engine ready");
session.PositionChanged += state =>
{
    // Safe to bind UI here because callbacks are marshaled to uiContext.
    Console.WriteLine(state.CurrentFen.Raw);
};
session.MoveMade += move => Console.WriteLine($"{move.Actor}: {move.Notation} [{move.KindFlags}]");
session.PromotionRequired += request => ShowPromotionButtons(request.AllowedPromotionPieces);
session.MoveClassified += move => Console.WriteLine($"Highlight {move.Notation}");
session.EvaluationChanged += pv => Console.WriteLine(pv.Raw);
session.EngineThinkingStarted += _ => ShowThinking(true);
session.EngineThinkingStopped += _ => ShowThinking(false);
session.ResultChanged += state => Console.WriteLine($"Result: {state.Result.Reason}");
session.DrawOffered += state => Console.WriteLine($"Draw offered by {state.DrawOfferedBy}");
session.ClockPaused += state => Console.WriteLine($"Clock paused: {state.Clock?.IsPaused}");

await session.UpdatePositionAsync(Fen.Default, null, cancellationToken);
await session.StartSearchAsync(cancellationToken);
```

### Prefer the compact typed event stream for new integrations
```csharp
using Bezoro.Chess.UCI.API;
using Bezoro.Chess.UCI.API.Types;
using Bezoro.Chess.UCI.Protocol.API.Types;

await using var session = await UciGameEngineSession.CreateAsync(enginePath, ct: cancellationToken);

session.EventPublished += sessionEvent =>
{
    switch (sessionEvent)
    {
        case { Kind: UciGameEngineSessionEventKind.MoveMade, Move: { } move }:
            Console.WriteLine($"{move.Actor}: {move.Notation}");
            break;

        case { Kind: UciGameEngineSessionEventKind.PositionChanged, State: { } state }:
            Console.WriteLine(state.CurrentFen.Raw);
            break;

        case { Kind: UciGameEngineSessionEventKind.PromotionRequired, PromotionRequired: { } request }:
            ShowPromotionButtons(request.AllowedPromotionPieces);
            break;
    }
};

await session.UpdatePositionAsync(Fen.Default, null, cancellationToken);
```

### Drive a promotion request/response flow
```csharp
using Bezoro.Chess.UCI.API;
using Bezoro.Chess.UCI.API.Common.Enums;
using Bezoro.Chess.UCI.Protocol.API.Types;

await using var session = await UciGameEngineSession.CreateAsync(enginePath, ct: cancellationToken);
await session.UpdatePositionAsync(Fen.Parse("1r5k/P7/8/8/8/8/8/K7 w - - 0 1")!.Value, null, cancellationToken);

PromotionRequiredEvent pending = default;
session.PromotionRequired += request => pending = request;

await session.MakeMoveAsync("a7a8", cancellationToken);
await session.ChoosePromotionAsync(pending.PendingPromotionId, PieceType.Queen, cancellationToken);
```

### Configure coordinator-level engine behavior
```csharp
using Bezoro.Chess.UCI.API;
using Bezoro.Chess.UCI.API.Types;
using Bezoro.Chess.UCI.Protocol.API.Types;

var options = new UciCoordinatorOptions(
    PonderThreads: 2,
    MultiPv: 3,
    ClassificationDepth: 8,
    EngineMoveTimeMs: 750,
    TimeControl: new PlayableMatchTimeControl(TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(2)),
    ClaimableDrawPolicy: PlayableMatchClaimableDrawPolicy.ClaimRequired,
    DrawOfferPolicy: PlayableMatchDrawOfferPolicy.ExpireOnMove,
    ControlledMoveFallbackPolicy: PlayableMatchControlledMoveFallbackPolicy.UseLocalFallback);

await using var session = await UciGameEngineSession.CreateAsync(
    enginePath,
    options,
    ct: cancellationToken);

await session.SetOptionAsync("Hash", "256", cancellationToken);
await session.SetDebugAsync(false, cancellationToken);
```

## API Reference
### `UciGameEngineSession`
| Member                                                            | Description                                                         |
|-------------------------------------------------------------------|---------------------------------------------------------------------|
| `CreateAsync(enginePath, options, syncContext, perspectiveColor, whiteController, blackController, ct)` | Constructs and starts a ready-to-use game-engine session. |
| `StartAsync(ct)` / `StopAsync(ct)`                                | Starts or stops all internal engine instances.                      |
| `ResetAsync(ct)` / `NewGameAsync(ct)`                             | Resets position state or starts a fresh engine game context.        |
| `UpdatePositionAsync(fen, playedMoves, ct)`                       | Synchronizes protocol-backed snapshot, ponder, and classification flows to a position. |
| `SetPositionAsync(fenString, ct)`                                 | Convenience wrapper that parses a FEN string.                       |
| `StartSearchAsync(ct)` / `StartSearchAsync(fen, playedMoves, ct)` | Starts infinite pondering search.                                   |
| `StopSearchAsync(ct)`                                             | Stops the current ponder search.                                    |
| `SearchAsync(parameters, ct)`                                     | Runs a bounded quick search and returns `SearchResult`.             |
| `PlayControlledMoveAsync(ct)`                                     | Plays the current side automatically when that side is engine-controlled. |
| `ClassifyMoveAsync(move, ct)`                                     | Returns semantic move analysis for one legal move.                  |
| `StreamClassifiedMovesAsync(ct)`                                  | Streams classifications as they complete.                           |
| `WaitForClassificationAsync(ct)`                                  | Waits for all legal moves to be classified.                         |
| `MakeMoveAsync(move, ct)` / `MakeMoveAsync(move, actor, ct)`      | Applies a legal move and refreshes state, optionally tagging the actor. |
| `ChoosePromotionAsync(id, pieceType, ct)`                         | Resolves a pending promotion request and applies the completed move. |
| `UndoAsync(count, ct)`                                            | Rewinds one or more played moves.                                   |
| `ResignAsync(ct)`                                                 | Ends the current game immediately by resignation for the side to move. |
| `OfferDrawAsync(ct)` / `AcceptDrawAsync(ct)` / `DeclineDrawAsync(ct)` / `ClaimDrawAsync(ct)` | Drives draw-offer and claimable-draw flows backed by protocol rules. |
| `PauseClockAsync(ct)` / `ResumeClockAsync(ct)`                    | Pauses or resumes the configured match clock.                       |
| `SetOptionAsync(name, value, ct)`                                 | Applies a UCI option to all internal engine instances.              |
| `SetDebugAsync(enabled, ct)`                                      | Broadcasts `debug on/off` to all internal engines.                  |
| `RegisterAsync(registration, ct)`                                 | Broadcasts the standard `register` command to all internal engines. |
| `GetCurrentFenAsync(ct)`                                          | Returns the engine-reported current FEN if available.               |
| `PerspectiveColor` / `WhiteController` / `BlackController`        | Controller-neutral playable-side configuration mirrored from protocol. |
| `GetController(side)`                                             | Returns the configured controller for `w` or `b`.                   |
| `State`                                                           | Current immutable snapshot.                                         |
| `EngineInfo` / `AvailableOptions` / `Capabilities`                | Handshake metadata surfaced from the snapshot client.               |
| `EventPublished`                                                   | Compact typed event stream for new integrations.                    |
| `GameStarted`                                                     | Raised when a fresh gameplay session starts.                        |
| `MoveMade`                                                        | Canonical rich move event for applied moves.                        |
| `CaptureMade` / `CastlingMade` / `EnPassantMade`                  | Convenience move projections for common UI hooks.                   |
| `PromotionRequired` / `PromotionChosen`                           | Promotion request/response events for UI-driven choice flows.       |
| `Check` / `Checkmate` / `Stalemated`                              | Tactical end-state events for applied moves.                        |
| `MoveUndone`                                                      | Raised when moves are undone.                                       |
| `IllegalMoveRejected`                                             | Raised before an illegal move call throws.                          |
| `ResultChanged`                                                   | Raised when the adjudicated match result changes.                   |
| `DrawOffered` / `DrawDeclined`                                    | Raised when a draw offer is published or cleared.                   |
| `ClockPaused` / `ClockResumed`                                    | Raised when the configured match clock is paused or resumed.        |
| `TurnChanged`                                                     | Raised when the active side changes.                                |
| `PositionLoaded` / `PositionChanged`                              | Raised for explicit loads and visible board snapshot updates.       |
| `LegalMovesUpdated`                                               | Raised when the visible legal move set changes.                     |
| `SearchStateChanged`                                              | Raised when the session enters or leaves searching state.           |
| `EngineThinkingStarted` / `EngineThinkingStopped`                 | Raised for UI-facing engine-think lifecycle transitions.            |
| `EvaluationChanged`                                                | Raised for new principal variations from the ponder engine.         |
| `EvaluationUpdated`                                                | Obsolete alias for `EvaluationChanged`; prefer `EvaluationChanged` or `EventPublished`. |
| `BestMoveChanged`                                                 | Raised when the current best/ponder move pair changes.              |
| `MoveClassified`                                                   | Raised for each newly classified legal move.                        |
| `MoveClassificationUpdated`                                       | Obsolete alias for `MoveClassified`; prefer `MoveClassified` or `EventPublished`. |
| `ClassificationCompleted`                                         | Raised when the current position's move classification is complete. |
| `GameOver`                                                        | Raised when the current position has no legal moves.                |
| `StateChanged`, `Ready`, `Stopped`, `Error`                       | Coarser lifecycle and integration events.                           |
| `EngineError`                                                     | Obsolete alias for `Error`; prefer `Error` or `EventPublished`.     |

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
| `Result` / `ClaimableResult`                          | Current terminal result or a draw that must still be claimed.  |
| `DrawOfferedBy`                                       | Side that currently has an unanswered draw offer, when any.    |
| `Clock`                                               | Current protocol-backed chess-clock snapshot, when configured. |
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
| `EngineMoveTimeMs`    | Move time budget used by `PlayControlledMoveAsync`. |
| `TimeControl`         | Optional protocol-side chess clock configuration. |
| `ClaimableDrawPolicy` | Whether repetition and fifty-move draws are automatic or require an explicit claim. |
| `DrawOfferPolicy`     | Whether pending draw offers expire on the next move. |
| `ControlledMoveFallbackPolicy` | Whether engine-controlled turns may fall back to a legal local move. |
| `Default`             | Safe default configuration.                   |

## Design Notes
- This project hides transport/protocol complexity behind one game-facing facade.
- The session now uses the protocol layer's controller-neutral side model, so the same facade can represent manual/manual, manual/engine, or engine/engine local sessions.
- The session exposes one canonical rich `MoveMade` payload plus convenience events so a game engine can react to board snapshots, search updates, move semantics, undo, promotion, and terminal states without reverse-engineering chess rules from raw UCI strings.
- New integrations should prefer `EventPublished`; focused subscriptions should use canonical named events such as `EvaluationChanged`, `MoveClassified`, and `Error`.
- Obsolete compatibility aliases (`EvaluationUpdated`, `MoveClassificationUpdated`, and `EngineError`) remain for source compatibility only.
- Resign, draw offer/claim, adjudicated result, and chess-clock state are projected from protocol-backed match rules rather than reimplemented by the consumer.
- The session builds on protocol-owned local FEN and legal-move generation for playable match flow, while engine-specific `d` and `go perft 1` remain available only as optional low-level escape hatches on `UciEngineClient`.
- Protocol types such as `Fen`, `SearchParameters`, and `SearchResult` come from `Bezoro.Chess.UCI.Protocol`; this project uses them rather than redefining them.
- Search and metadata snapshots exposed by the protocol layer are immutable, so session state can safely retain and rebroadcast them across threads.
