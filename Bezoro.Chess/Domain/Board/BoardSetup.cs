namespace Bezoro.Chess.Domain.Board
{
	/// <summary>
	///     Utility class for creating initial game states with different board setups.
	/// </summary>
	public static class BoardSetup
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
			GameState.CreateInitial();

		/// <summary>
		///     Creates a game state with the standard chess starting position where Black moves first.
		/// </summary>
		public static GameState CreateStandardGameBlackStarts() =>
			GameState.CreateInitial(PieceColor.Black);
	}
}
