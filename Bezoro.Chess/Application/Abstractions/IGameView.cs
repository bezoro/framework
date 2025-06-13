using System.Collections.Generic;
using Bezoro.Chess.Application.Abstractions.ViewModels;

namespace Bezoro.Chess.Application.Abstractions
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
