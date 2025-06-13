# Bezoro Chess Engine Architecture

## Overview

The Bezoro Chess Engine is a comprehensive chess implementation built using Clean Architecture with the
Model-View-Presenter (MVP) pattern as the presentation layer approach. It is designed as a cross-platform solution
compatible with iOS, Android, and PC platforms. The core chess logic (Domain and Application components) is developed in
a separate .NET library, enabling clean separation of concerns, thorough testing, and platform independence.

## Current Implementation Status

The project has successfully implemented the core chess logic components including:

- Complete chess rules engine with all special moves
- Immutable game state management
- Move generation and validation
- FEN notation support for position serialization
- MVP architecture with well-defined boundaries
- Comprehensive test coverage

## Project Structure

The Bezoro Chess Engine follows Clean Architecture principles and is organized into several layers:

```
Bezoro.Chess/
├── Domain/                 # Core chess rules and entities
│   ├── Board/              # Board-related entities and helpers
│   │   ├── Piece.cs
│   │   ├── Position.cs
│   │   ├── BoardHelper.cs
│   │   ├── BoardSetup.cs
│   │   └── PieceColorExtensions.cs
│   ├── Moves/              # Move representation and validation
│   │   └── Move.cs
│   ├── Rules/              # Chess rules implementation
│   │   ├── GameStatusChecker.cs
│   │   └── GameManagerHelper.cs
│   ├── GameState.cs        # Immutable game state
│   └── GameStateExtensions.cs
├── Application/            # Application services and use cases
│   ├── Abstractions/       # Interfaces for external dependencies
│   │   └── IGameView.cs
│   ├── Generators/         # Move generation for each piece type
│   │   ├── MoveGenerator.cs
│   │   └── PieceMoveGenerators/
│   ├── MoveExecution/      # Implementation of move execution
│   │   └── MoveExecutors/
│   ├── GameManager.cs      # Main game flow coordinator
│   ├── GamePresenter.cs    # MVP presenter component
│   └── ViewModels/         # DTOs for the UI layer
│       ├── PieceViewModel.cs
│       └── HighlightViewModel.cs
├── Infrastructure/         # External concerns implementation
│   ├── Persistence/        # Game storage implementations
│   └── Serialization/      # FEN and PGN parsers
```

## Core Architecture Components

### Domain Layer

The Domain layer contains the core business logic and entities, independent of any frameworks or external concerns.

#### Game State Management

- **GameState** - Immutable record representing a complete chess position
- **GameStateExtensions** - Adds core domain operations to GameState like `IsSquareAttackedBy` and `FindKingPosition`
- **GameStatusChecker** - Evaluates terminal conditions like checkmate and draws

#### Board Representation

- **Position** - Represents a square on the chess board (row, column)
- **Piece** - Value type representing a chess piece with type and color
- **Move** - Represents a chess move with from/to positions and special flags
- **BoardHelper** - Utilities for board operations like `IsInsideBoard`
- **BoardSetup** - Factory for creating game states with different board configurations
- **PieceColorExtensions** - Extensions for piece colors, including the `Opposite` method

### Application Layer

The Application layer contains use cases, application services, and interfaces to external dependencies.

#### Game Management

- **GameManager** - Manages game flow, history, and state transitions
- **GameManagerHelper** - Utility functions for game state management

#### Move Generation

- **MoveGenerator** - Entry point for generating all possible moves
- **Piece-specific Generators** - Specialized generators for each piece type:
    - PawnMoveGenerator
    - KnightMoveGenerator
    - BishopMoveGenerator
    - RookMoveGenerator
    - QueenMoveGenerator
    - KingMoveGenerator
- **SlidingPieceMoveGenerator** - Shared logic for sliding pieces (bishop, rook, queen)

#### Move Execution

- **MoveExecution** - Factory coordinating the execution of different move types
- **Specialized Executors**:
    - NormalMoveExecutor
    - EnPassantMoveExecutor
    - CastleMoveExecutor
    - PawnPromotionMoveExecutor

### Infrastructure Layer

#### FEN Support

- **FenParser** - Parses and generates FEN notation for chess positions
- **FenStringConstants** - Constants for FEN notation

### Presentation Layer (MVP)

#### Presenter

The Presenter acts as a mediator between the Application and View layers, handling user input and updating the model
accordingly.

- **GamePresenter** - Main coordinator that manages game flow and interactions
- **Abstractions**:
    - **IGameView** - Interface that UI implementations must implement
- **ViewModels** - Data transfer objects for the UI:
    - PieceViewModel
    - HighlightViewModel

#### View (Unity Implementation)

The View component is responsible for visual representation and user interaction. Implemented with Unity as the primary
UI framework, with a clean separation using assembly definitions.

##### Special Unity Folders

- **Editor/** - Contains all editor-only scripts and tools that extend the Unity Editor
    - Custom inspectors for game components
    - Board setup and configuration tools
    - Editor windows for game management
    - Editor-only assembly definition (`_Project.Editor.asmdef`)

- **Plugins/** - Contains third-party plugins and native code
    - External Unity assets
    - Platform-specific native plugins

- **Resources/** - Contains assets that need to be loaded at runtime via `Resources.Load()`
    - Dynamic configuration files
    - Runtime-loaded textures and prefabs
    - Note: Use sparingly as all Resources assets are included in builds

```
Assets/
└── _Project/
    ├── Art/               # Visual assets (sprites, materials)
    │   ├── Sprites/
    │   └── Materials/
    ├── Audio/             # Sound effects and music
    │   ├── Music/
    │   └── SFX/
    ├── Editor/           # Editor-only scripts
    │   ├── CustomInspectors/
    │   ├── Tools/
    │   │   └── BoardSetupTool.cs
    │   └── _Project.Editor.asmdef
    ├── Libraries/
    │   └── Bezoro.Chess.dll  # Core chess logic library
    ├── Plugins/          # Third-party plugins and native code
    │   └── README.md     # Documentation for included plugins
    ├── Prefabs/           # Unity prefabs for board and pieces
    │   ├── Pieces/
    │   └── UI/
    ├── Resources/        # Assets loaded at runtime via Resources.Load
    │   ├── Configurations/
    │   └── Textures/
    ├── Scenes/            # Unity scenes
    │   ├── MainGame.unity
    │   └── MainMenu.unity
    ├── ScriptableObjects/ # Configuration assets
    │   ├── GameConfig.asset
    │   └── PieceTheme.asset
    └── Scripts/
        ├── Runtime/       # Application bootstrap code
        │   ├── GameBootstrapper.cs
        │   └── _Project.Runtime.asmdef
        └── View/          # UI implementation
            ├── GameView.cs
            ├── BoardSquare.cs
            ├── PieceView.cs
            └── _Project.View.asmdef
```

## Key Workflows

### Move Generation and Validation

1. `MoveGenerator` identifies all pseudo-legal moves for a given position
2. `GameManager.IsMoveLegal()` checks if a move would leave the king in check
3. `GameManager.GetLegalMoves()` combines these to produce only valid moves

### Move Execution

1. `GameManager.TryMakeMove()` attempts to make a player's move
2. The appropriate move executor is selected based on move type
3. A new immutable GameState is created with the move applied
4. Game history is updated and any end-of-game conditions are checked

### UI Interaction

1. User selects a piece through `GamePresenter.OnSquareSelected()`
2. Legal moves are highlighted in the UI via `IGameView.UpdateMoveHighlights()`
3. User selects a destination square
4. Move is executed and board is updated via `IGameView.UpdateBoard()`

## Design Patterns

The Bezoro Chess Engine implements several design patterns to achieve a clean and maintainable architecture:

- **Clean Architecture** - Organizes code into concentric layers (Domain, Application, Infrastructure, Presentation)
- **Model-View-Presenter (MVP)** - Separates presentation logic from UI implementation
- **Immutable State** - GameState is immutable to prevent state corruption
- **Factory Method** - Used for move executors, move generators, and board setup
- **Strategy Pattern** - Different strategies for move generation/execution
- **Chain of Responsibility** - Evaluation of game end conditions
- **Dependency Inversion** - Application core doesn't depend on external frameworks

## Cross-Platform Considerations

### Core Architecture

- **Platform-Agnostic Logic**: All core chess logic in C# compatible with .NET Standard 2.1 and .NET 9.0
- **Unity as Cross-Platform Layer**: Leverages Unity's cross-platform capabilities
- **Abstraction Layers**: Abstracts hardware and OS-specific functionality

### Platform-Specific Optimizations

- **iOS & Android**: Optimized touch input, responsive layout, and efficient memory usage
- **PC**: Support for mouse and keyboard input with enhanced visual features

## Testing Strategy

The codebase is extensively tested with unit and integration tests:

### Core Library Tests

- **Unit Tests** - Test individual components in isolation
    - Move generation for each piece type
    - Legal move validation
    - Special move rules (castling, en passant, etc.)

- **Integration Tests** - Test interactions between components
    - Complete game flows
    - End game detection
    - UI presenter interaction

### Unity Tests

The Unity implementation is tested using the Unity Test Framework with a clear separation between test code and game
code:

```
Assets/
├── _Project/
│   └── ... (game code)
└── Tests/
    ├── EditMode/
    │   ├── Application/
    │   │   └── GamePresenterTests.cs
    │   ├── Domain/
    │   │   └── GameStateTests.cs
    │   └── _Project.Tests.EditMode.asmdef
    ├── PlayMode/
    │   ├── View/
    │   │   └── GameViewTests.cs
    │   └── _Project.Tests.PlayMode.asmdef
    └── Editor/
        ├── CustomInspectorTests.cs
        ├── EditorToolTests.cs
        └── _Project.Tests.Editor.asmdef
```

- **Edit Mode Tests** - Run in the Unity Editor without entering Play mode
    - Test the GamePresenter with mock views
    - Verify view model construction

- **Play Mode Tests** - Run in Play mode or on device
    - Test GameView rendering and user interaction
    - Verify MonoBehaviour-dependent functionality

    - **Editor Tests** - Specifically test editor extensions
    - Test custom inspectors and property drawers
    - Verify editor tools and windows function correctly
    - These tests run in Edit mode but specifically target Editor-only code

## Current Strengths

- **Clean Architecture**: Well-structured code with clear separation of concerns
- **Immutable Value Types**: Thread-safe state management preventing unwanted mutations
- **Comprehensive Testing**: Extensive test coverage ensuring correctness
- **Flexibility**: Extensible design allowing for variants and custom rules
- **Strong Domain Modeling**: Accurate representation of chess concepts
- **FEN Support**: Standard chess position notation support

## Future Improvements

### Immediate Roadmap

- Complete Unity view implementation
- Optimizations for cross-platform performance
- Enhanced visual feedback for moves and game state

### Future Features

- Chess engine AI implementation
- Opening book support
- PGN import/export
- Time control management
- Network play support
- Stage-based learning system with tutorials

### Planned Educational Features

- **Predefined Move Sequences**: For teaching standard openings and tactics
- **Tutorial System**: Interactive guidance for learning chess concepts
- **Stage Progression**: Gradually increasing challenge across learning stages
- **Hint System**: Contextual hints for players learning the game

## Implementation Plan

The implementation follows a strict test-driven development approach where:

1. Comprehensive tests are written first to define expected behavior
2. Core functionality is implemented to pass the tests
3. Code is refactored for cleanliness and performance while maintaining test coverage
4. User interface is built on top of the tested core components

### Unity Implementation Workflow

1. Create a Unity project with proper assembly definitions:
    - `_Project.View.asmdef` - Contains all MonoBehaviour implementations
    - `_Project.Runtime.asmdef` - Contains bootstrap/composition code

2. Reference the compiled Bezoro.Chess.dll in both assemblies

3. Create a main bootstrapper component:
   ```csharp
   public class GameBootstrapper : MonoBehaviour
   {
       [SerializeField] private GameView gameView;

       private GamePresenter _presenter;
       private GameManager _gameManager;

       private void Start()
       {
           _gameManager = new GameManager();
           _presenter = new GamePresenter(_gameManager, gameView);
       }
   }
   ```

4. Implement the `IGameView` interface in a Unity-specific way:
   ```csharp
   public class GameView : MonoBehaviour, IGameView
   {
       [SerializeField] private Transform boardContainer;
       [SerializeField] private BoardSquare squarePrefab;
       [SerializeField] private PieceView piecePrefab;

       // Implementation of IGameView methods
   }
   ```

    5. Create editor tools to improve development workflow:
   ```csharp
   [CustomEditor(typeof(GameView))]
   public class GameViewEditor : Editor
   {
       public override void OnInspectorGUI()
       {
           base.OnInspectorGUI();

           var gameView = (GameView)target;

           if (GUILayout.Button("Reset Board"))
           {
               // Editor-only functionality
               gameView.ResetBoard();
           }
       }
   }
   ```

    6. Test the implementation using the Unity Test Framework:

    - Edit Mode tests for presenter logic
    - Play Mode tests for Unity-specific components
    - Editor tests for custom inspectors and tools

## Conclusion

The Bezoro Chess Engine demonstrates solid software design principles through its implementation of Clean Architecture
with the MVP pattern. With its clear separation of concerns, immutable state management, and extensive test coverage, it
provides a strong foundation for chess applications across multiple platforms.

The architecture successfully balances the needs for correctness, performance, and maintainability, with a clear
boundary between the core chess logic and the Unity implementation. This approach ensures the code remains flexible
enough to support future extensions such as AI opponents, network play, and educational features, while also allowing
for comprehensive testing of both the core logic and the Unity-specific components.
