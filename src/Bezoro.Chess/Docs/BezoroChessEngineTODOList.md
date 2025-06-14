# Project TODO List

This file tracks the remaining high-priority tasks and completed milestones for the Bezoro Chess Engine project.

- [x] **Core Chess Logic**
    - [x] Board representation
    - [x] Move generation and validation
        - [x] Rook
            - [x] Normal
            - [x] Capture
        - [x] Bishop
            - [x] Normal
            - [x] Capture
        - [x] Queen
            - [x] Normal
            - [x] Capture
        - [x] King
            - [x] Normal
            - [x] Capture
            - [x] Castling
        - [x] Pawn
            - [x] Normal
            - [x] Capture
            - [x] En Passant
            - [x] Promotion
    - [x] Complete standard chess rules

- [x] **Game State Management**
    - [x] Immutable game state with history tracking
    - [x] Game state history with undo/redo functionality
    - [x] Game outcome detection
        - [x] Check
        - [x] Checkmate
        - [x] Stalemate
        - [ ] Forfeit
    - [x] Draw conditions
        - [x] 50-move rule
        - [x] Threefold repetition
        - [x] Insufficient material

- [x] **Architecture and Presentation**
    - [x] MVP architecture with well-defined view interfaces
    - [x] Presenter layer implementation

- [x] **Notation Support**
    - [x] SAN (Standard Algebraic Notation) support for moves
    - [x] FEN parsing support for setting up games

- [ ] **Complete Unity View Implementation**
    - [ ] Implement the `IGameView` interface in Unity
    - [ ] Create the board visualization system
    - [ ] Design piece movement animations

- [ ] **Add FEN Support**
    - [ ] Complete FEN parsing and generation for position serialization
    - [ ] Add position import/export functionality

- [ ] **Implement Basic AI**
    - [ ] Create a simple computer opponent
    - [ ] Implement multiple difficulty levels

- [ ] **Platform Optimizations**
    - [ ] Implement cross-platform optimizations for iOS, Android, and PC.
