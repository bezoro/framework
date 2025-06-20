using System;
using System.Collections.Generic;
using Bezoro.Chess.API.ViewModels;

namespace Bezoro.Chess.API.Abstractions
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
		///     Hides the game settings interface.
		/// </summary>
		void HideSettingsUI();

		/// <summary>
		///     Highlights the last move made on the board by visually marking the source and destination squares.
		/// </summary>
		/// <param name="lastMove">
		///     A tuple containing the source square (from) and destination square (to) coordinates in algebraic
		///     notation
		/// </param>
		void HighlightLastMove((string from, string to) lastMove);

		/// <summary>
		///     Shows a confirmation dialog to the player.
		/// </summary>
		/// <param name="message">The message to display</param>
		/// <param name="confirmCallback">Callback to execute when the user confirms</param>
		/// <param name="cancelCallback">Callback to execute when the user cancels</param>
		void ShowConfirmationDialog(string message, Action confirmCallback, Action cancelCallback);

		/// <summary>
		///     Shows the game results screen with detailed statistics.
		/// </summary>
		/// <param name="gameResult">The result of the game</param>
		/// <param name="statistics">Game statistics to display</param>
		void ShowGameResults(string gameResult, Dictionary<string, string> statistics);

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
		///     Shows the game settings interface to configure game options.
		/// </summary>
		void ShowSettingsUI();

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

		/// <summary>
		///     Shows the move history in the UI.
		/// </summary>
		/// <param name="moveHistory">Collection of moves to display</param>
		void UpdateMoveHistory(IEnumerable<string> moveHistory);
	}
}
