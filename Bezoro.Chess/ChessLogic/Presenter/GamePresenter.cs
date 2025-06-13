using System;
using System.Collections.Generic;
using System.Linq;
using Bezoro.Chess.ChessLogic.Generators;
using Bezoro.Chess.ChessLogic.Presenter.ViewModels;

namespace Bezoro.Chess.ChessLogic.Presenter
{
	/// <summary>
	///     The main coordinator that manages game flow, user interactions, and updates the View.
	///     It acts as the bridge between the Model (GameState) and the View (IGameView).
	/// </summary>
	public class GamePresenter : IUserInputHandler
	{
		public GamePresenter(IGameView view)
		{
			_view                       = view;
			_legalMovesForSelectedPiece = Enumerable.Empty<Move>();
		}

		private          GameState         _gameState;
		private          IEnumerable<Move> _legalMovesForSelectedPiece;
		private readonly IGameView         _view;
		private          Position?         _selectedPosition;

	#region Interface Implementations

		public void OnSquareSelected(Position position)
		{
			var pieceAtPosition = _gameState.GetPieceAt(position);

			// If a piece is already selected
			if (_selectedPosition.HasValue)
			{
				var move = _legalMovesForSelectedPiece.FirstOrDefault(m => m.To == position);

				// Case 1: The user clicked a legal move destination
				if (move != default)
				{
					_gameState = _gameState.ExecuteMove(move);
					ClearSelection();
					UpdateViewBoard();
				}
				// Case 2: The user clicked a different piece of their own color
				else if (pieceAtPosition != default && pieceAtPosition.Color == _gameState.ActiveColor)
				{
					SelectPiece(position);
				}
				// Case 3: The user clicked an invalid square or the same square
				else
				{
					ClearSelection();
				}
			}
			// If no piece is selected yet
			else if (pieceAtPosition != default && pieceAtPosition.Color == _gameState.ActiveColor)
			{
				// Case 4: The user selects one of their pieces
				SelectPiece(position);
			}
		}

		public void OnPromotionPieceSelected(PieceType pieceType) =>
			// This will be implemented in a future step when we test pawn promotion.
			throw new NotImplementedException();

	#endregion

		public void StartNewGame()
		{
			_gameState = GameState.CreateInitial();
			ClearSelection();
			UpdateViewBoard();
		}

		private void ClearSelection()
		{
			_selectedPosition           = null;
			_legalMovesForSelectedPiece = Enumerable.Empty<Move>();
			_view.HighlightLegalMoves(Enumerable.Empty<Position>());
		}

		private void SelectPiece(Position position)
		{
			_selectedPosition           = position;
			_legalMovesForSelectedPiece = MoveGenerator.GeneratePieceMoves(position, _gameState);
			_view.HighlightLegalMoves(_legalMovesForSelectedPiece.Select(m => m.To));
		}

		private void UpdateViewBoard()
		{
			var viewModel = new PieceViewModel[8, 8];
			for (var row = 0 ; row < 8 ; row++)
			{
				for (var col = 0 ; col < 8 ; col++)
				{
					var piece = _gameState.PiecePositions[row, col];
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
