# Bezoro Chess Engine

A lightweight, **pure-logic** chess library designed for integration into any host environment (Unity, Godot, console
applications, servers, etc.).

This library contains **no dependencies** on `UnityEngine`, `Godot.*`, file-system, networking, or any other I/O
systems, making it completely portable.

## Technical Specifications

* Frameworks:
	* `netstandard2.1` – Library (stable)
	* `net9.0-preview` – Tests (preview)
### Language Versions
	* C# 9.0 – Library (stable)
	* C# 13.0-preview – Tests (preview)

### Deployment
* Drop-in library for any compatible .NET host

---

## Design Principles

### Core Architecture

* **Immutable Domain** – All **internal** value objects/records are immutable (primarily `readonly struct`s)
* **Public-API Convenience** – The façade may expose *mutable* DTOs for ease-of-use, but it converts them to immutable domain types before invoking core logic
* **Pure Functions** – Functions receive state and return new state with no hidden mutations
* **One-Way Coupling** – Dependencies flow downward only, eliminating cyclic references
* **Data-Oriented Design** – Cache-friendly layouts and minimal branching in hot paths
* **Composition Over Inheritance** – Favors functional composition and plain data over OOP hierarchies
* **Library-Only Approach** – Host applications handle all I/O concerns (input, rendering, persistence, threading)
* **Memory Efficiency** – Minimizes GC pressure through value types and object pooling
* **Performance Optimizations** – Strategic use of `MethodImpl(MethodImplOptions.AggressiveInlining)` on hot paths

### Board Representation

* **Linear Array** – The chess board is represented as a 64-element array (indices 0-63)
* **Coordinate System**:
	- A1 = index 0
	- H8 = index 63
	- Files (A-H) determine columns
	- Ranks (1-8) determine rows

---

## Repository Structure

```
Bezoro.Framework.sln
└─ src/
   └─ Bezoro.Chess/
       ├─ Domain/                 # Core chess logic and domain model
       │   ├─ Shared/             # Enums, constants, and shared types
       │   ├─ Types/              # Immutable value objects
       │   ├─ Functions/          # Pure functional systems
       │   ├─ Helpers/            # Domain-specific utility functions
       │   └─ Extensions/         # Extension methods for domain types
       │
       ├─ API/                    # Public interface layer
       │   ├─ Engine/             # Main engine and entry points
       │   ├─ Abstractions/       # Interfaces and contracts
       │   ├─ Extensions/         # API extension methods
       │   ├─ Helpers/            # API-specific utilities
       │   └─ ViewModels/         # Data transfer objects
       │
       ├─ Docs/                   # Internal documentation and design decisions
       └─ Bezoro.Chess.csproj
│
└─ tests/
    └─ Bezoro.Chess.Tests/
        ├─ Unit/                  # Unit tests
        ├─ Integration/           # Integration tests
        ├─ Properties/            # Property-based tests
        ├─ Performance/           # Performance benchmarks
        └─ Multi-thread/          # Multi-threading tests
```

---

## Architecture Overview

```
┌─────────────────────────┐
│                         │
│  Host Application       │
│  (Unity/Godot/etc.)     │
│                         │
└───────────┬─────────────┘
            │
            ▼
┌─────────────────────────┐
│                         │
│  Bezoro.Chess           │
│  Public API Façade      │
│                         │
└───────────┬─────────────┘
            │
            ▼
┌─────────────────────────┐
│                         │
│  Chess Domain Layer     │
│  (Core Chess Logic)     │
│                         │
└───────────┬─────────────┘
            │
            ▼
┌─────────────────────────┐
│                         │
│  Bezoro.Core            │
│  (Shared Utilities)     │
│                         │
└─────────────────────────┘
```

This layered architecture ensures separation of concerns and maintains a clean dependency flow from the host application
down through the layers.

---

## Domain Layer Details

> 100% internal

### Shared Components

#### Enumerations (`Domain/Shared/Enums.cs`)

| Enum               | Values                                                                                    | Description                         |
|--------------------|-------------------------------------------------------------------------------------------|-------------------------------------|
| `PieceType`        | `None`, `Pawn`, `Knight`, `Bishop`, `Rook`, `Queen`, `King`                               | Types of chess pieces               |
| `PieceColor`       | `None`, `White`, `Black`                                                                  | Color of chess pieces               |
| `SquareCoordinate` | `None`, `A1`...`H8`                                                                       | Standard chess board coordinates    |
| `MoveType`         | `None`, `Quiet`, `Capture`, `Castling`, `QuietPromotion`, `CapturePromotion`, `EnPassant` | Classifications of chess moves      |
| `CastlingRights`   | `None`, `WhiteKingside`, `WhiteQueenside`, `BlackKingside`, `BlackQueenside`, `All`       | Bit-flags for castling availability |
| `FailureReason`    | `None`, `TriedMovingFromEmptySquare`, etc.                                                | Error conditions for move attempts  |

#### Constants (`Domain/Shared/Consts.cs`)

| Constant         | Description                                          |
|------------------|------------------------------------------------------|
| `AttackVectors`  | Pre-computed directional vectors for each piece type |
| `FENStrings`     | Standard Forsyth-Edwards Notation starting positions |
| `BoardConstants` | Mapping constants (e.g., A1 = SquareCoordinate.A1)   |

### Domain Types

> Immutable value objects and aggregates containing data without behavior

#### Value Types (`Domain/Types/Structs/`)

| Type      | Properties                                                                                             | Description                                      |
|-----------|--------------------------------------------------------------------------------------------------------|--------------------------------------------------|
| `Piece`   | `PieceColor Color`<br>`PieceType Type`                                                                 | Represents a chess piece with its color and type |
| `Square`  | `PieceType Occupant`<br>`uint Rank` (derived)<br>`uint File` (derived)<br>`char RankChar` (derived)    | Represents a single square on the chess board    |
| `Move`    | `SquareCoordinate From`<br>`SquareCoordinate To`<br>`MoveType Type`<br>`CastlingRights CastlingRights` | Represents a chess move with all necessary data  |
| `Board`   | `ReadOnlySpan<Square> Squares`                                                                         | Represents the entire chess board state          |
| `FenData` | `ReadOnlySpan<Square> Pieces`<br>`CastlingRights Castling`<br>...                                      | Forsyth-Edwards Notation data structure          |

#### Reference Types (`Domain/Types/Records/`)

| Type        | Properties                                                                                                            | Description                                   |
|-------------|-----------------------------------------------------------------------------------------------------------------------|-----------------------------------------------|
| `GameState` | `Board Board`<br>`SquareCoordinate EnPassantSquare`<br>`uint HalfmoveClock`<br>`CastlingRights CastlingRights`<br>... | Complete representation of a chess game state |

### Functions (`Domain/Functions/...`)

Pure systems that operate on types; Applies changes and generates new state

**Domain/Functions/MoveGeneration/**

| File                      | Description                                                    |
|---------------------------|----------------------------------------------------------------|
| `GeneratePseudoMoves.cs`  | (in GameState) Generates **all** pseudo moves for all pieces   |
| `…MoveGenerator.cs`       | Individual piece move generators used by `GeneratePseudoMoves` |
| `FilterLegalMoves.cs`     | (in Moves[]) Remove moves that leave own king in check         |
| `GenerateIllegalMoves.cs` | (in GameState) Generates all **illegal** moves                 |

**Domain/Functions/MoveApplication/**

| File                   | Description                                                               |
|------------------------|---------------------------------------------------------------------------|
| `ApplyMove.cs`         | (in GameState, in Move move) – applies `move` and returns a new GameState |
| `...MoveApplicator.cs` | Individual piece move applicator used by `ApplyMove`                      |

**Domain/Functions/Undo/**

| File                 | Description                                |
|----------------------|--------------------------------------------| 
| `UndoLastMove.cs`    | Reverts the game state to previous move    |
| `RedoLastMove.cs`    | Replays a previously undone move           |
| `TrimUndoHistory.cs` | Cleans up/removes old undo history entries |

**Domain/Functions/Outcome/**

| File                        | Description                                                    |
|-----------------------------|----------------------------------------------------------------|
| `ProcessDraw.cs`            | Processes stalemate, threefold, 50-move, insufficient material |
| `ProcessVictory.cs`         | Processes win by checkmate or concede                          |
| `CreateGameStateFromFEN.cs` | Creates a `GameState` from a valid FEN string                  |
| `GameStateToUCI.cs`         | Converts `GameState` to UCI move string                        |
| `CheckHandling.cs`          | Detects check / checkmate                                      |

### Utility Components

#### Helper Functions (`Domain/Helpers/...`)

| Helper      | Purpose                                                      |
|-------------|--------------------------------------------------------------|
| `FenParser` | Parses Forsyth-Edwards Notation strings into structured data |
| `UCIParser` | Parses Universal Chess Interface commands                    |
| `PGNParser` | Parses Portable Game Notation files                          |

#### Extension Methods (`Domain/Extensions/...`)

| Extension          | Methods                                                                                       | Purpose                              |
|--------------------|-----------------------------------------------------------------------------------------------|--------------------------------------|
| `PieceExtensions`  | `Is(PieceColor color, PieceType type)`                                                        | Piece type/color checking            |
| `SquareExtensions` | `IsOccupied()`<br>`Algebraic()`                                                               | Square state and notation conversion |
| `BoardExtensions`  | `GetPieceAt(SquareCoordinate)`<br>`GetSquareAt(SquareCoordinate)`<br>`GetSquareIndex(Square)` | Board navigation and lookup          |

---

## Public API

The public-facing interface layer that exposes functionality to host applications. While still prioritizing functional
approaches, this layer allows for more OOP patterns where they benefit API consumers.

### Engine Component (`API/Engine/...`)

The primary entry point for host applications is `ChessEngine.cs`. All methods are asynchronous (`ValueTask<T>`) to
support future AI integration. Methods follow a `Try` pattern returning `Result<T>` with success/failure status and
relevant data.

| Method                                                         | Purpose                                             |
|----------------------------------------------------------------|-----------------------------------------------------|
| `TryGetLegalMovesForPosition(string position)`                 | Returns legal moves for a position                  |
| `TryGetPseudoLegalMovesForPosition(string position)`           | Returns pseudo-legal moves for a position           |
| `TryGetIllegalMovesForPosition(string position)`               | Returns illegal moves for a position (for UI hints) |
| `TryApplyMove(string from, string to)`                         | Attempts to execute a move between positions        |
| `TryApplyPromotion(string position, PieceType promotionPiece)` | Handles pawn promotion                              |
| `TrySerializePGNtoJSON(string pgn)`                            | Converts PGN to JSON format                         |

### Data Transfer Objects (`API/ViewModels/...`)

Immutable objects that transfer domain state to host applications.

| ViewModel            | Properties                                                                                                                    | Purpose                                   |
|----------------------|-------------------------------------------------------------------------------------------------------------------------------|-------------------------------------------|
| `MoveViewModel`      | `MoveType Type`<br>`CapturedPiece Captured`<br>`SquareCoordinate From`<br>`SquareCoordinate To`<br>...                        | Represents a chess move for API consumers |
| `BoardViewModel`     | `Squares Squares`                                                                                                             | Represents board state for API consumers  |
| `GameStateViewModel` | `SquareCoordinate[] Pieces`<br>`CastlingRights CastlingRights`<br>`uint HalfmoveClock`<br>`SquareCoordinate EnPassant`<br>... | Complete game state for API consumers     |

### Thread Safety

All API calls are **pure** and can be performed off the main thread. The host application controls threading and
synchronization.

---

## Testing Strategy

### Test Categories

| Test Type             | Location                                | Purpose                                        |
|-----------------------|-----------------------------------------|------------------------------------------------|
| **Unit Tests**        | `tests/Bezoro.Chess.Tests/Unit`         | Tests individual components in isolation       |
| **Integration Tests** | `tests/Bezoro.Chess.Tests/Integration`  | Tests interactions between components          |
| **Property Tests**    | `tests/Bezoro.Chess.Tests/Properties`   | Tests invariant properties using random inputs |
| **Performance Tests** | `tests/Bezoro.Chess.Tests/Performance`  | Benchmarks critical code paths                 |
| **Threading Tests**   | `tests/Bezoro.Chess.Tests/Multi-thread` | Verifies behavior under concurrent access      |

### Coverage Goals

* Core move generation and rule logic: ≥90% code coverage
* Integration test scenarios include: castling, promotion, threefold repetition, en passant, etc.

---

## Usage Examples

### Basic Integration

```csharp
// Initialize the chess engine with a receiver for events
var engine = new ChessEngine(receiver);
var controller = new GameController(engine);

// Start a new game with standard position
var newGameResult = await controller.TryStartNewGame();
if (!newGameResult.Success)
    return; // Handle initialization failure

// Make a move (pawn from A2 to A4)
var moveResult = await controller.TryApplyMove(SquareCoordinate.A2, SquareCoordinate.A4);
if (!moveResult.Success)
    return; // Handle illegal move

// Process move events
foreach (var e in moveResult.Events.Span)
{
    if (e is PieceMoved pm)
        Console.WriteLine($"{pm.From}->{pm.To}");
}
```

### Supporting Controller

```csharp
public sealed class GameController
{
    private readonly IChessEngine _engine;

    public GameController(IChessEngine engine) => _engine = engine;

    public async ValueTask<GameStateViewModel> TryStartNewGame()
    {
        // Standard starting position in FEN notation
        const string fen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
        return await _engine.NewGameAsync(fen);
    }

    public async ValueTask<MoveAttempt> TryApplyMove(SquareCoordinate from, SquareCoordinate to)
    {
        return await _engine.TryApplyMoveAsync(from, to);
    }
}
```

## Versioning Strategy

This project follows [Semantic Versioning 2.0.0](https://semver.org/):

| Version Component | Incremented When                                     |
|-------------------|------------------------------------------------------|
| **MAJOR**         | Making incompatible API changes                      |
| **MINOR**         | Adding functionality in a backward-compatible manner |
| **PATCH**         | Making backward-compatible bug fixes                 |

## Future Enhancements

* **Bitboard Implementation** - For improved performance
* **AI Integration** - Chess engine with configurable difficulty levels
* **Opening Book Support** - Standard chess openings database
* **PGN Import/Export** - Full support for chess notation standards
* **Position Evaluation** - Static evaluation of board positions