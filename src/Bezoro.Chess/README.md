# Simple Chess Engine

A simple **pure-logic** chess library--for personal use--that can be dropped into any host (Unity, Godot, console app,
server, …).  
There is **zero** reference to `UnityEngine`, `Godot.*`, file-system, networking, or any other I/O.

Technical Limitations: (Unity support)
* Target framework`netstandard2.1`
* Language version C# 9.0
---

## Design Principles

* **Immutable domain** – every value object and record is immutable. Ideally readonly structs.
* **Pure functions** – receive state, return new state; no hidden mutation.
* **One-way coupling** – callers only depend on downward; no cyclic calls.
* **Data-Oriented where hot** – SoA, cache-friendly layout, minimal branching inside tight loops.
* **Minimal to No OOP** – favor composition and plain data.
* **Library-only** – hosts handle input, view, persistence, threading.
* **Minimal GC pressure** – favor value types and pooling.
* **Aggressive inlining** – heavy use of MethodImpl(MethodImplOptions.AggressiveInlining) for hot paths.

Coordinate system:

- Board represented as a linear array (0–63)
- A1 = index 0
- H8 = index 63
- File (A-H) determines column
- Rank (1-8) determines row

---

## Repository Layout

```
Bezoro.Framework.sln
└─ src
   └─ Bezoro.Chess/
       ├─ Domain/            ← 100% internal
       │   ├─ Shared/        ← enums & constants
       │   ├─ Types/         ← value objects & aggregates, no logic, only data
       │   ├─ Functions/     ← pure systems (Parsing, Move, Outcome)
       │   └─ Extensions/    ← extension helpers for domain types
       ├─ API/               ← public façade & engine-facing contracts
       │   ├─ Interfaces/    ← contracts for API consumers
       │   └─ ViewModels/    ← immutable data transfer objects
       └─ Bezoro.Chess.csproj
└─ tests
   └─ Bezoro.Chess.Tests/
       ├─ Unit/          ← unit tests
       ├─ Integration/   ← integration tests
       ├─ Properties/    ← property-based tests
       ├─ Performance/   ← performance tests
       └─ Multi-thread/  ← multi-threading tests 
```

---

## Project Dependencies

```
Unity / Godot (host)/ ...
          │
          ▼
    (Bezoro.Chess)
      API façade
          │
          ▼
        Domain
          │
          ▼
    (Bezoro.Core)
```

---

## Domain Folder Details

### Shared/ – Core code used across the domain

* **Enums.cs**
	* `PieceType` – `None, Pawn, Knight, Bishop, Rook, Queen, King`
	* `PieceColor` – `None, White, Black`
	* `SquarePosition` – `None, A1 … H8`
	* `CastlingRights` – bit-flags (`WhiteKingside`, `WhiteQueenside`, …, `All`)
	* `CastleSide` – `None, BlackKing, BlackQueen, WhiteKing, WhiteQueen`

* **Consts.cs** – static readonly & const
	* `AttackVectors` – pre-computed directional vectors for each piece type.
	* `FENStrings` – Standard FEN string setups
	* `BoardConstants` – A1 = SquarePosition.A1, ...

### Types/ – Immutable data objects

* **Structs/** (readonly)
	* `Piece.cs(PieceColor color, PieceType type)`
	* `Square.cs(PieceType, uint file, uint rank)`
	* `Move.cs(SquarePosition source, SquarePosition destination)`
	* `Board.cs(ImmutableArray<Square> squares)` – Exposed as a ReadOnlySpan getter
	* `FenData.cs()`
* **Records/** (reference types are to be avoided)
	* `GameState.cs(FenData fen)`

### Functions/ – Applies changes and generates new state

* **Hot/** – performance critical
	* **MoveGeneration/**
		* `GeneratePseudoMoves.cs(in GameState)` – all pseudo-legal moves (en passant, castling, promotion).
		* `…MoveGenerator.cs` – one generator per piece type.
		* `FilterLegalMoves.cs(in GameState, in ImmutableArray<Move> moves)` – remove moves leaving own king in check.

	* `MakeMove.cs(in GameState, in Move move)` – apply a move and return new state.

* **Support/** – helpers
	* **Parsing/**
		* `FENToGameState.cs(string)` – create a `GameState` from FEN.
		* `GameStateToUCI.cs(in GameState)` – convert game state to UCI move string.
		* `PGNSerialization.cs(in GameState)` – serialise to PGN-like JSON.

	* **Rules Compliance/**
		* `CheckHandling.cs(in GameState)` – detect check / checkmate.

	* **Outcome/**
		* `DrawHandling.cs(in GameState)` – stalemate, threefold, 50-move, insufficient material.
		* `VictoryHandling.cs(in GameState)` – win by checkmate or resignation.

	* **Undo/**
		* `UndoLastMove.cs(in GameState)`
		* `RedoLastMove.cs(in GameState)`
		* `TrimUndoHistory.cs(in GameState)`

### Extensions/ – Facilitate common read operations

* `PieceExtensions.cs` – `Is(PieceColor color, PieceType type)`
* `SquareExtensions.cs` – `IsOccupied()`, `Algebraic()`
* `BoardExtensions.cs` – `GetPieceAt`, `GetSquareAt`, `GetSquareIndex`

---

## API Folder

Public façade consumed by host engines. OOP is allowed--but still not encouraged, here.

* **Engine/**
	* `ChessEngine.cs` – The entrypoint to the entire system
		* `void ReceiveInput(InputViewModel)`
		* `Task<MoveViewModel[]> GetLegalMovesForPosition(GameStateViewModel, string)`
		* `Task<MoveViewModel[]> GetPseudoLegalMovesForPosition(GameStateViewModel, string)`
		* `Task<MoveViewModel[]> GetIllegalMovesForPosition(GameStateViewModel, string)`

* **Interfaces/**
	* `IMakeMoveResultReceiver.cs`
		* `MoveViewModel Receive()`

	* `IGameStateUpdateReceiver.cs`
		* `GameStateViewModel Receive()`

	* ...

* **ViewModels/** (readonly structs)
	* `InputViewModel.cs` – input data object
	* `GameStateViewModel.cs` – full match state data object
	* `BoardViewModel.cs` – board state data object
	* `MoveViewModel.cs` – move state data object
	* ...

Thread-safety: all API calls are **pure** and can be performed off the main thread; the host decides where to invoke
them.

---

## Testing (xUnit)

* Unit tests in `tests/Bezoro.Chess.Tests/Unit`.
* Coverage goal ≥ 90 % for core move generation and rule logic.
* Integration tests in `tests/Bezoro.Chess.Tests/Integration`.
	* Castling
	* Promotion
	* Threefold
	* ...
* Optional: Performance tests is `tests/Bezoro.Chess.Tests/Perf`.
* Optional: Multi-threading tests is `tests/Bezoro.Chess.Tests/Multi-thread`.

---

## Quick Start Example

```csharp
// ── Entry point ────────────────────────────────────────────────────────────────
var receiver   = new ConsoleMoveReceiver();
var engine  = new ChessEngine(receiver);          // single composition root
var controller = new GameController(engine);

var game = controller.StartNewGame();                   // ← app logic
controller.MakeMove(game, (A2, A4))
// ── Supporting types ──────────────────────────────────────────────────────────
sealed class GameController
{
    readonly IChessEngine _engine;

    public GameController(IChessEngine engine) => 
        _engine = engine;

    public async Task<GameStateViewModel> StartNewGame()
    {
        const string fen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
        await _engine.NewGame(fen);
    }
    
    public async Task<MoveViewModel> MakeMove(GameStateViewModel game, (SquarePosition from, SquarePosition to))
    {
        await _engine.MakeMoveAsync(game, (from, to));
    }
}

sealed class ConsoleMoveReceiver : IMakeMoveResultReceiver
{
    public ValueTask Receive(MoveViewModel m, CancellationToken _ = default) =>
        new(Console.WriteLine($"{m.Source} → {m.Destination}"));
}
```

## Versioning Strategy

- Semantic Versioning (MAJOR.MINOR.PATCH)
	- MAJOR version for incompatible API changes
	- MINOR version for backwards-compatible functionality
	- PATCH version for backwards-compatible bug fixes

## Wishlist

- Bitboard
- AI
