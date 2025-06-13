using System.Collections.Generic;
using Bezoro.Chess.ChessLogic.Presenter.ViewModels;

namespace Bezoro.Chess.ChessLogic.Presenter
{
	public interface IGameView
	{
		/// <summary>
		///     Updates the entire board with the latest piece positions.
		/// </summary>
		void UpdateBoard(PieceViewModel[,] board);

		/// <summary>
		///     Highlights squares on the board to show the player where a selected piece can move.
		/// </summary>
		void UpdateMoveHighlights(IEnumerable<MoveHighlightViewModel> highlights);
	}
}
