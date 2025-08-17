using Bezoro.Chess.Domain.Shared.Enums;
using Bezoro.Chess.Domain.Types.Records;

namespace Bezoro.Chess.Domain.Helpers
{
	/// <summary>
	///     Utility class for creating initial game states with different board setups.
	/// </summary>
	internal static class BoardSetup
	{
		/// <summary>
		///     Creates an empty game state with no pieces on the board.
		/// </summary>
		public static GameState CreateEmptyBoard() =>
			new();

		/// <summary>
		///     Creates a game state with the standard chess starting position where White moves first.
		/// </summary>
		public static GameState CreateStandardGame() =>
			GameState.CreateInitial(PieceColor.White);

		/// <summary>
		///     Creates a game state with the standard chess starting position where Black moves first.
		/// </summary>
		public static GameState CreateStandardGameBlackStarts() =>
			GameState.CreateInitial(PieceColor.Black);
	}
}
