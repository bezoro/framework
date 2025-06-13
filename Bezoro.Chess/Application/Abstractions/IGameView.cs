using System.Collections.Generic;
using Bezoro.Chess.Application.Abstractions.ViewModels;

namespace Bezoro.Chess.Application.Abstractions
{
	public interface IGameView
	{
		/// <summary>
		///     Hides any currently displayed message.
		/// </summary>
		void HideMessage();

		/// <summary>
		///     Hides the pawn promotion interface after the player has made their selection.
		/// </summary>
		void HidePromotionUI();

		/// <summary>
		///     Displays a message to the player, typically used for game notifications or errors.
		/// </summary>
		/// <param name="message">The message text to display</param>
		void ShowMessage(string message);

		/// <summary>
		///     Displays the pawn promotion interface when a pawn reaches the opposite end of the board.
		/// </summary>
		void ShowPromotionUI();

		/// <summary>
		///     Updates the entire board with the latest piece positions.
		/// </summary>
		void UpdateBoard(PieceViewModel[,] board);

		/// <summary>
		///     Updates the game status display with current turn, game result, check status, and captured pieces.
		/// </summary>
		/// <param name="gameStatus">View model containing the current game status information</param>
		void UpdateGameStatus(GameStatusViewModel gameStatus);

		/// <summary>
		///     Highlights squares on the board to show the player where a selected piece can move.
		/// </summary>
		void UpdateMoveHighlights(IEnumerable<MoveHighlightViewModel> highlights);
	}
}
