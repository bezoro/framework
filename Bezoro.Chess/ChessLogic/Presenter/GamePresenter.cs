using System;
using System.Collections.Generic;
using System.Linq;
using Bezoro.Chess.ChessLogic.Presenter.ViewModels;

namespace Bezoro.Chess.ChessLogic.Presenter
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
		private          Position?                              _selectedPosition;

	#region Interface Implementations

		public void OnSquareSelected(Position position)
		{
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
					if (_gameManager.TryMakeMove(moveTuple.move))
					{
						ClearSelection();
						UpdateViewBoard();
					}
					else
					{
						// This case should ideally not be reached if the logic is sound.
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
					// The user clicked an invalid square (empty or opponent's piece), so clear the selection.
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

		public void OnPromotionPieceSelected(PieceType pieceType) => throw new NotImplementedException();

	#endregion

		public void StartNewGame()
		{
			_gameManager.NewGame();
			ClearSelection();
			UpdateViewBoard();
		}

		private void ClearSelection()
		{
			_selectedPosition      = null;
			_movesForSelectedPiece = Enumerable.Empty<(Move move, bool isLegal)>();
			_view.UpdateMoveHighlights(Enumerable.Empty<MoveHighlightViewModel>());
		}

		private void OnGameEnded(GameOutcome outcome)
		{
			// TODO: Handle the end of the game in the view
		}

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

		private void UpdateViewBoard()
		{
			var viewModel = new PieceViewModel[8, 8];
			for (var row = 0 ; row < 8 ; row++)
			{
				for (var col = 0 ; col < 8 ; col++)
				{
					var piece = _gameManager.CurrentState.PiecePositions[row, col];
					if (piece != default)
					{
						viewModel[row, col] = new(piece.Type, piece.Color);
					}
				}
			}

			_view.UpdateBoard(viewModel);
		}
	}
}
