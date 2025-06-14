using System.Collections.Generic;
using System.Linq;
using Bezoro.Chess.Application.Abstractions;
using Bezoro.Chess.Application.Abstractions.ViewModels;
using Bezoro.Chess.Domain.Board;
using Bezoro.Chess.Domain.Moves;
using Bezoro.Chess.Domain.Notation;

namespace Bezoro.Chess.Application.Features.PlayGame
{
	public class GamePresenter : IUserInputHandler
	{
		public GamePresenter(IGameView view)
		{
			_view                  =  view;
			_gameManager           =  new();
			_movesForSelectedPiece =  Enumerable.Empty<(Move move, bool isLegal)>();
			_gameManager.GameEnded += OnGameEnded;
		}

		private readonly GameManager                            _gameManager;
		private          IEnumerable<(Move move, bool isLegal)> _movesForSelectedPiece;
		private readonly IGameView                              _view;
		private          Move?                                  _pendingPromotionMove;
		private          Position?                              _selectedPosition;

		public GameManager GameManager => _gameManager;

	#region Interface Implementations

		/// <summary>
		///     Handles the selection of a square on the chess board, managing piece selection,
		///     move execution, and board state updates based on the player's interaction.
		/// </summary>
		/// <param name="position">The position of the selected square on the chess board.</param>
		/// <remarks>
		///     This method implements the following behavior:
		///     - If a piece is already selected:
		///     * Re-selecting the same piece deselects it
		///     * Selecting a valid move destination executes the move
		///     * Selecting another friendly piece changes selection to that piece
		///     * Selecting an invalid square clears the selection
		///     - If no piece is selected:
		///     * Selecting a friendly piece initiates piece selection
		/// </remarks>
		public void OnSquareSelected(Position position)
		{
			// If we are waiting for a promotion choice, ignore board clicks until a piece is chosen.
			if (_pendingPromotionMove.HasValue)
			{
				_view.ShowMessage("Please select a piece for promotion.");
				return;
			}

			var pieceAtPosition = _gameManager.CurrentState.GetPieceAt(position);

			if (_selectedPosition.HasValue)
			{
				// If the user re-clicks the selected piece, deselect it.
				if (_selectedPosition.Value.Equals(position))
				{
					ClearSelection();
					return;
				}

				var moveTuple = _movesForSelectedPiece.FirstOrDefault(m => m.move.To == position);

				// Check if a move to the selected square exists and is legal
				if (moveTuple != default && moveTuple.isLegal)
				{
					// Check for pawn promotion
					if (moveTuple.move.Type is MoveType.PawnPromotion or MoveType.PawnPromotionCapture)
					{
						_pendingPromotionMove = moveTuple.move;
						_view.ShowPromotionUI();
						return; // Wait for user to select promotion piece
					}

					if (_gameManager.TryMakeMove(moveTuple.move))
					{
						ClearSelection();
						UpdateView();
					}
					else
					{
						// This case should ideally not be reached if the logic is sound.
						_view.ShowMessage("An unexpected error occurred with the move.");
						ClearSelection();
					}
				}
				// If the user clicks another of their own pieces, switch selection to that piece.
				else if (pieceAtPosition.Type  != PieceType.None &&
						 pieceAtPosition.Color == _gameManager.CurrentState.ActiveColor)
				{
					SelectPiece(position);
				}
				else
				{
					// The user clicked an invalid square (empty or opponent's piece).
					_view.ShowMessage("Invalid move.");
					ClearSelection();
				}
			}
			// If no piece is selected yet, and the clicked square has a piece of the active color.
			else if (pieceAtPosition.Type  != PieceType.None &&
					 pieceAtPosition.Color == _gameManager.CurrentState.ActiveColor)
			{
				SelectPiece(position);
			}
		}

		public void OnPromotionPieceSelected(PieceType pieceType)
		{
			if (!_pendingPromotionMove.HasValue)
			{
				// This should not be reached in normal gameplay.
				return;
			}

			// Complete the promotion move with the selected piece type.
			var  pendingMove = _pendingPromotionMove.Value;
			Move promotionMove;

			if (pendingMove.Type == MoveType.PawnPromotion)
			{
				promotionMove = Move.CreateQuietPromotion(
					pendingMove.From, pendingMove.To, pendingMove.Piece, pieceType);
			}
			else // Assumes PawnPromotionCapture
			{
				promotionMove = Move.CreateCapturePromotion(
					pendingMove.From, pendingMove.To, pendingMove.Piece, pendingMove.CapturedPiece, pieceType);
			}

			if (_gameManager.TryMakeMove(promotionMove))
			{
				ClearSelection();
				UpdateView();
			}
			else
			{
				// This case should not be reached if the initial move was valid.
				_view.ShowMessage("An error occurred during promotion.");
				ClearSelection();
			}

			_view.HidePromotionUI();
			_pendingPromotionMove = null;
		}

	#endregion

		public void OnForfeitRequested()
		{
			// ???: Here we could add logic to ask the user for confirmation via the view
			_gameManager.Forfeit();
			_view.ShowMessage($"{_gameManager.CurrentState.ActiveColor} forfeits.");
		}

		public void OnHideSettings() =>
			_view.HideSettingsUI();

		public void OnShowSettings() =>
			_view.ShowSettingsUI();

		public void StartNewGame()
		{
			if (_gameManager.Outcome == GameOutcome.Ongoing && _gameManager.MoveHistory.Any())
			{
				_view.ShowConfirmationDialog(
					"A game is in progress. Are you sure you want to start a new one?",
					() =>
					{
						_gameManager.NewGame();
						ClearSelection();
						UpdateView();
					},
					() => { } // On cancel, do nothing
				);
			}
			else
			{
				_gameManager.NewGame();
				ClearSelection();
				UpdateView();
			}
		}

		private void ClearSelection()
		{
			_selectedPosition      = null;
			_movesForSelectedPiece = Enumerable.Empty<(Move move, bool isLegal)>();
			_view.UpdateMoveHighlights(Enumerable.Empty<MoveHighlightViewModel>());
			_view.HideMessage();
		}

		private void OnGameEnded(GameOutcome outcome)
		{
			UpdateView(); // Update the view to show the final board and status.

			var statistics = new Dictionary<string, string>
			{
				{ "Outcome", outcome.ToString() },
				{ "Total Moves", _gameManager.MoveHistory.Count.ToString() }
			};

			_view.ShowGameResults(outcome.ToString(), statistics);
		}

		/// <summary>
		///     Selects a chess piece at the specified position and updates the view with available moves.
		///     This method retrieves all possible moves for the selected piece, determines their legality,
		///     and displays appropriate highlights on the board for each potential move.
		/// </summary>
		/// <param name="position">The position of the piece to be selected on the chess board.</param>
		private void SelectPiece(Position position)
		{
			_selectedPosition      = position;
			_movesForSelectedPiece = _gameManager.GetMovesWithLegalityForPiece(position).ToList();

			var highlights = _movesForSelectedPiece.Select(
				m =>
					new MoveHighlightViewModel(
						m.move.To,
						m.isLegal ? MoveHighlightType.Legal : MoveHighlightType.Illegal
					));

			_view.UpdateMoveHighlights(highlights);
		}

		/// <summary>
		///     Updates the entire view with the latest game state, including the board layout and game status.
		/// </summary>
		private void UpdateView()
		{
			// Update the board with the current piece positions.
			var boardViewModel = new PieceViewModel[8, 8];
			for (var row = 0 ; row < 8 ; row++)
			{
				for (var col = 0 ; col < 8 ; col++)
				{
					var piece = _gameManager.CurrentState.PiecePositions[row, col];
					if (piece != default)
					{
						boardViewModel[row, col] = new(piece.Type, piece.Color);
					}
				}
			}

			_view.UpdateBoard(boardViewModel);

			// Highlight the last move if there is one
			var lastMove = _gameManager.MoveHistory.LastOrDefault();
			if (lastMove.Type != MoveType.None)
			{
				_view.HighlightLastMove(lastMove.From, lastMove.To);
			}

			// Update the move history display
			_view.UpdateMoveHistory(
				_gameManager.MoveHistory
							.Select(
								(move, idx) =>
									move.ToSAN(_gameManager.GameStateHistory[idx])));

			// Update the game status display.
			_gameManager.CapturedPieces.TryGetValue(PieceColor.White, out var whitePieces);
			var whiteCaptured = (whitePieces ?? Enumerable.Empty<Piece>())
								.Select(p => new PieceViewModel(p.Type, p.Color)).ToList();

			_gameManager.CapturedPieces.TryGetValue(PieceColor.Black, out var blackPieces);
			var blackCaptured = (blackPieces ?? Enumerable.Empty<Piece>())
								.Select(p => new PieceViewModel(p.Type, p.Color)).ToList();

			var statusViewModel = new GameStatusViewModel(
				_gameManager.CurrentState.ActiveColor,
				_gameManager.Outcome.ToString(),
				_gameManager.IsKingInCheck(_gameManager.CurrentState, _gameManager.CurrentState.ActiveColor),
				whiteCaptured,
				blackCaptured
			);

			_view.UpdateGameStatus(statusViewModel);
		}
	}
}
