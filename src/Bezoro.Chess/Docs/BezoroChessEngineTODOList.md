# Project TODO List

This file tracks all behavioral goals for the Bezoro Chess Engine project, with remaining work prioritized at the top of
each section.

### Domain (Core Chess Logic)

- [x] **Model the complete state of a chess game.**
  - [x] Track the position of every piece, whose turn it is, and castling rights.
  - [x] Track special game states like en passant availability and the 50-move rule clock.
- [x] **Determine every legal move a player can make.**
  - [x] Calculate all possible moves for each piece type (Pawn, Rook, Knight, etc.).
  - [x] Correctly generate special moves: castling, en passant, and pawn promotion.
  - [x] Ensure a move is truly legal by checking if it would expose the player's king to an attack.
- [x] **Manage the flow and history of the game.**
  - [x] Apply a move to the board to produce the next game state.
  - [x] Keep a complete history of moves and states to allow for undo and redo.
- [x] **Determine the final outcome of the game.**
  - [x] Detect when a player is in check, has been checkmated, or the game is a stalemate.
  - [x] Allow a player to forfeit, resulting in a loss.
- [x] **Enforce all standard draw conditions.**
  - [x] Trigger a draw for a repeated board position (threefold repetition).
  - [x] Trigger a draw after 50 moves without a pawn advance or a capture.
  - [x] Trigger a draw when neither player has enough pieces to force a checkmate.

### Presenter

- [x] **Handle all player interactions.**
  - [x] When a player selects their own piece, determine its available moves.
  - [x] When a player selects a destination, validate and execute the move.
  - [x] Manage complex move sequences, like waiting for a pawn promotion choice.
  - [x] Handle requests to start a new game, including asking for confirmation if a game is in progress.
- [x] **Prepare game data for the user interface.**
  - [x] Translate the core game state into a simpler visual format for the UI to display.
  - [x] Tell the UI which squares to highlight to show legal moves.
  - [x] Provide lists of captured pieces, game status, and move history in a display-ready format.
- [x] **Support standard chess notations.**
  - [x] Convert internal game moves into Standard Algebraic Notation (SAN) for the move list.
  - [x] Allow a game to be started from a Forsyth-Edwards Notation (FEN) string.

### View (User Interface & Interaction)

- [ ] **Board & Piece Rendering**
  - [ ] Draw the chessboard and render the pieces in their correct positions.
  - [ ] Animate piece movements from one square to another.
  - [ ] Replace placeholder graphics with final art assets for the board and pieces.
- [ ] **Player Interaction & Feedback**
  - [ ] Allow a player to select their pieces by clicking on them.
  - [ ] Visually highlight the squares a selected piece can legally move to.
  - [ ] Provide clear visual feedback when a player attempts an illegal move.
- [ ] **UI Elements & Game Information**
  - [ ] Display whose turn it is and if a player is in check.
  - [ ] Show lists of pieces that have been captured by each player.
  - [ ] Display player timers and a total move counter.
  - [ ] Show a pop-up UI for the player to choose a piece during pawn promotion.
  - [ ] Display a dedicated screen announcing the game's winner or reason for a draw.
- [ ] **Game Setup**
  - [ ] Provide UI elements (e.g., a text box and button) for starting a game from a FEN string.

### Sound

- [ ] Play a sound when a piece moves or captures.
- [ ] Play a distinct warning sound for check.
- [ ] Play sounds to signify the end of a game (win, lose, draw).

### AI Opponent

- [ ] **AI Integration**
  - [ ] Allow an AI to take the place of a human player.
  - [ ] Integrate the AI into the main game loop so it takes its turn automatically.
- [ ] **AI Behavior**
  - [ ] Create a "Level 1" AI that makes random, but legal, moves.
  - [ ] Create a "Level 2" AI that uses a basic board evaluation (like material count) to choose better moves.

### Long-Term & Additional Features

- [ ] **Persistence**
  - [ ] Allow a game to be saved to a file in PGN (Portable Game Notation) format.
  - [ ] Allow a saved PGN game to be loaded and continued.
- [ ] **Testing**
- [ ] Test gameplay logic and rules enforcement:
  - [ ] Test basic piece moves (all piece types)
  - [ ] Test special moves (castling, en passant, promotion)
  - [ ] Test move validation and king check detection
  - [ ] Test game outcomes (checkmate, stalemate, draws)
- [ ] Test game state management:
  - [ ] Test move history and undo/redo
  - [ ] Test board state after each move
  - [ ] Test captured pieces tracking
- [ ] Test auxiliary features:
  - [ ] Test FEN string import/export
  - [ ] Test PGN save/load
  - [ ] Test algebraic notation generation
- [ ] **Settings & Customization**
  - [ ] Allow players to choose from different visual themes (e.g., board color, piece style).
  - [ ] Provide options to turn sounds on or off.