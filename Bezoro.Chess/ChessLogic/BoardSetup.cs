namespace Bezoro.Chess.ChessLogic
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
		///     Creates a game state with the standard chess starting position.
		/// </summary>
		public static GameState CreateStandardGame()
		{
			var gameState = new GameState();

			// Setup white pieces on the 8th rank (row 7)
			gameState.PiecePositions[7, 0] = new(PieceType.Rook, PieceColor.White);
			gameState.PiecePositions[7, 1] = new(PieceType.Knight, PieceColor.White);
			gameState.PiecePositions[7, 2] = new(PieceType.Bishop, PieceColor.White);
			gameState.PiecePositions[7, 3] = new(PieceType.Queen, PieceColor.White);
			gameState.PiecePositions[7, 4] = new(PieceType.King, PieceColor.White);
			gameState.PiecePositions[7, 5] = new(PieceType.Bishop, PieceColor.White);
			gameState.PiecePositions[7, 6] = new(PieceType.Knight, PieceColor.White);
			gameState.PiecePositions[7, 7] = new(PieceType.Rook, PieceColor.White);

			// Setup white pawns on the 7th rank (row 6)
			for (var col = 0 ; col < 8 ; col++)
			{
				gameState.PiecePositions[6, col] = new(PieceType.Pawn, PieceColor.White);
			}

			// Setup black pieces on the 1st rank (row 0)
			gameState.PiecePositions[0, 0] = new(PieceType.Rook, PieceColor.Black);
			gameState.PiecePositions[0, 1] = new(PieceType.Knight, PieceColor.Black);
			gameState.PiecePositions[0, 2] = new(PieceType.Bishop, PieceColor.Black);
			gameState.PiecePositions[0, 3] = new(PieceType.Queen, PieceColor.Black);
			gameState.PiecePositions[0, 4] = new(PieceType.King, PieceColor.Black);
			gameState.PiecePositions[0, 5] = new(PieceType.Bishop, PieceColor.Black);
			gameState.PiecePositions[0, 6] = new(PieceType.Knight, PieceColor.Black);
			gameState.PiecePositions[0, 7] = new(PieceType.Rook, PieceColor.Black);

			// Setup black pawns on the 2nd rank (row 1)
			for (var col = 0 ; col < 8 ; col++)
			{
				gameState.PiecePositions[1, col] = new(PieceType.Pawn, PieceColor.Black);
			}

			return gameState;
		}
	}
}
