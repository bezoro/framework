using System.Collections.Generic;
using Bezoro.Chess.ChessLogic.Presenter.ViewModels;

namespace Bezoro.Chess.ChessLogic.Presenter
{
	/// <summary>
	///     Implemented by the View (e.g., Unity) to receive updates from the Presenter.
	///     This interface defines how the Presenter can command the View to change what is displayed.
	/// </summary>
	public interface IGameView
	{
		/// <summary>
		///     Instructs the View to highlight the squares where a selected piece can legally move.
		/// </summary>
		/// <param name="legalMoves">A collection of positions to highlight.</param>
		void HighlightLegalMoves(IEnumerable<Position> legalMoves);

		/// <summary>
		///     Instructs the View to display the final result of the game (e.g., Checkmate, Stalemate).
		/// </summary>
		/// <param name="message">The result message to display.</param>
		void ShowGameResult(string message);

		/// <summary>
		///     Instructs the View to show a promotion choice UI for the specified color.
		/// </summary>
		/// <param name="forColor">The color of the pawn being promoted.</param>
		void ShowPromotionChoice(PieceColor forColor);

		/// <summary>
		///     Instructs the View to render the board based on the provided piece view models.
		/// </summary>
		/// <param name="board">A 2D array representing the pieces on the board.</param>
		void UpdateBoard(PieceViewModel[,] board);
	}
}
