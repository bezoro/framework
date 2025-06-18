# Simple Chess Engine

A simple **pure-logic** chess library--for personal use--that can be dropped into any host (Unity, Godot, console app,
server, …).  
There is **zero** reference to `UnityEngine`, `Godot.*`, file-system, networking, or any other I/O.

* Target framework`netstandard2.1`
* Language version C# 9.0
* Build`dotnet build src/Bezoro.Chess -c Release`
* Tests`dotnet test tests/Bezoro.Chess.Tests`

---

## Design Principles

* **Immutable domain** – every value object and record is immutable.
* **Pure functions** – receive state, return new state; no hidden mutation.
* **One-way coupling** – callers only depend on downward; no cyclic calls.
* **Data-Oriented where hot** – SoA, cache-friendly layout, minimal branching inside tight loops.
* **Minimal to No inheritance** – favour composition and plain data.
* **Library-only** – hosts handle input, view, persistence, threading.

---

## Repository Layout

```
Bezoro.Framework.sln
└─ src
   └─ Bezoro.Chess/
       ├─ Domain/
       │   ├─ Shared/        ← enums & constants
       │   ├─ Types/         ← value objects & aggregates, no logic, only data
       │   ├─ Functions/     ← pure systems (Parsing, Move, Outcome)
       │   └─ Extensions/    ← extension helpers for domain types
       ├─ API/               ← public façade & engine-facing contracts
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
Unity / Godot (host)
          │
          ▼
      API façade
          │
          ▼
   Bezoro.Chess (this repo)
          │
          ▼
    Bezoro.Core (utilities)
```

---

## Domain Folder Details

### Shared/

* **Enums.cs**
	* `PieceType` – `None, Pawn, Knight, Bishop, Rook, Queen, King`
	* `PieceColor` – `None, White, Black`
	* `SquarePosition` – `None, A1 … H8`
	* `CastlingRights` – bit-flags (`WhiteKingside`, `WhiteQueenside`, …, `All`)
	* `CastleSide` – `None, BlackKing, BlackQueen, WhiteKing, WhiteQueen`

* **Consts.cs**
	* `AttackVectors` – pre-computed directional vectors for each piece type.

### Types/

* **Structs/** (readonly)
	* `Piece(PieceColor color, PieceType type)`
	* `Square(Piece occupant, uint file, uint rank)`
	* `Move(SquarePosition source, SquarePosition destination)`
	* `Board(Square[] squares)`
	* `FenData()`
	* `GameState(FenData fen)`
* **Records/** (reference types are to be avoided)
	* Just in case

### Functions/

* **Parsing/**
	* `FENToGameState(string fen)` – create a `GameState` from FEN.
	* `GameStateToUCI(GameState)` – convert game state to UCI move string.
	* `PGNSerialization(GameState)` – serialise to PGN-like JSON.

* **MoveGeneration/**
	* `GeneratePseudoMoves(in GameState)` – all pseudo-legal moves (en passant, castling, promotion).
	* `Piece/`
		* `…MoveGenerator` – one generator per piece type.
	* `MakeMove(in GameState, Move move)` – apply a move and return new state.
* **Rules Compliance/**
	* `FilterLegalMoves(in GameState, Move[] moves)` – remove moves leaving own king in check.
	* `CheckHandling(in GameState)` – detect check / checkmate.

* **Outcome/**
	* `DrawHandling(in GameState)` – stalemate, threefold, 50-move, insufficient material.
	* `VictoryHandling(in GameState)` – win by checkmate or resignation.

### Extensions/

* `PieceExtensions` – `Is(PieceColor color, PieceType type)`
* `SquareExtensions` – `IsOccupied()`, `Algebraic()`
* `BoardExtensions` – `GetPieceAt`, `GetSquareAt`, `GetSquareIndex`

---

## API Folder

Public façade consumed by host engines. OOP is allowed--but still not encouraged, here.

* **Interfaces/**
	* `IMakeMoveResultReceiver`
		* `MoveViewModel Receive()`

	* `IGameStateViewReceiver`
		* `GameStateViewModel Receive()`

	* ...

* **ViewModels/** (immutable record classes)
	* `GameStateViewModel` - full match state
	* `BoardViewModel`
	* `MoveViewModel` - Info of a specific move
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
// Assuming IMakeMoveResultReceiver
var chessPresenter = new ChessPresenter();
var game = chessPresenter.NewGame("rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1");
var move = chessPresenter.CreateMove("a2", "a4");
chessPresenter.MakeMove(game, move);
public void Receive(MoveViewModel move) => Console.WriteLine(move.Source, move.Destination);
```