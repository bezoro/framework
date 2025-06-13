using System;
using Bezoro.Chess.Domain.Board;
using Bezoro.Chess.Domain.Moves;

namespace Bezoro.Chess.Domain
{
	[Flags]
	public enum CastlingRights
	{
		None           = 0,
		WhiteKingside  = 1 << 0,
		WhiteQueenside = 1 << 1,
		BlackKingside  = 1 << 2,
		BlackQueenside = 1 << 3,
		WhiteBoth      = WhiteKingside | WhiteQueenside,
		BlackBoth      = BlackKingside | BlackQueenside,
		All            = WhiteBoth     | BlackBoth
	}

	public record GameState
	{
		/// <summary>
		///     Creates a game state with the standard chess starting position.
		/// </summary>
		/// <param name="startingColor">The color of the player whose turn it is to move.</param>
		public static GameState CreateInitial(PieceColor startingColor = PieceColor.White)
		{
			var gameState = new GameState
			{
				ActiveColor = startingColor,
				PiecePositions =
				{
					// Setup white pieces
					[7, 0] = new(PieceType.Rook, PieceColor.White),
					[7, 1] = new(PieceType.Knight, PieceColor.White),
					[7, 2] = new(PieceType.Bishop, PieceColor.White),
					[7, 3] = new(PieceType.Queen, PieceColor.White),
					[7, 4] = new(PieceType.King, PieceColor.White),
					[7, 5] = new(PieceType.Bishop, PieceColor.White),
					[7, 6] = new(PieceType.Knight, PieceColor.White),
					[7, 7] = new(PieceType.Rook, PieceColor.White),

					// Setup black pieces
					[0, 0] = new(PieceType.Rook, PieceColor.Black),
					[0, 1] = new(PieceType.Knight, PieceColor.Black),
					[0, 2] = new(PieceType.Bishop, PieceColor.Black),
					[0, 3] = new(PieceType.Queen, PieceColor.Black),
					[0, 4] = new(PieceType.King, PieceColor.Black),
					[0, 5] = new(PieceType.Bishop, PieceColor.Black),
					[0, 6] = new(PieceType.Knight, PieceColor.Black),
					[0, 7] = new(PieceType.Rook, PieceColor.Black)
				}
			};

			// Setup pawns for both colors
			for (var col = 0 ; col < 8 ; col++)
			{
				gameState.PiecePositions[6, col] = new(PieceType.Pawn, PieceColor.White);
				gameState.PiecePositions[1, col] = new(PieceType.Pawn, PieceColor.Black);
			}

			return gameState;
		}

		/// <summary>
		///     A bitmask representing the current castling availability.
		/// </summary>
		public CastlingRights Castling { get; init; } = CastlingRights.All;

		/// <summary>
		///     The number of full moves in the game. Starts at 1 and increments after Black moves.
		/// </summary>
		public int FullMoveNumber { get; init; } = 1;

		/// <summary>
		///     The number of half-moves since the last capture or pawn advance.
		///     Used for the fifty-move rule.
		/// </summary>
		public int HalfMoveClock { get; init; }
		/// <summary>
		///     The 8x8 grid of pieces. This IS the board.
		///     It's the single source of truth for all piece positions.
		/// </summary>
		public Piece[,] PiecePositions { get; init; } = new Piece[8, 8];

		/// <summary>
		///     The color of the player whose turn it is to move.
		/// </summary>
		public PieceColor ActiveColor { get; init; } = PieceColor.White;

		/// <summary>
		///     If a pawn has just made a two-square move, this is the position "behind" the pawn.
		///     This is needed for en passant capture validation.
		/// </summary>
		public Position? EnPassantTargetSquare { get; init; }

		public GameState ExecuteMove(Move move) =>
			MoveExecution.ExecuteMove(this, move);

		public Piece GetPieceAt(Position position) => PiecePositions[position.Row, position.Col];
	}
}
