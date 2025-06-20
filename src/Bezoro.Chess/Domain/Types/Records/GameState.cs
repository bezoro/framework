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
		White          = WhiteKingside | WhiteQueenside,
		Black          = BlackKingside | BlackQueenside,
		All            = White         | Black
	}

	internal record GameState
	{
		private static readonly (int, int)[] BishopAttackVectors = { (-1, -1), (-1, 1), (1, -1), (1, 1) };
		private static readonly (int, int)[] KingAttackVectors =
			{ (-1, -1), (-1, 0), (-1, 1), (0, -1), (0, 1), (1, -1), (1, 0), (1, 1) };

		// Attack vectors for non-sliding pieces
		private static readonly (int, int)[] KnightAttackVectors =
			{ (-2, -1), (-2, 1), (-1, -2), (-1, 2), (1, -2), (1, 2), (2, -1), (2, 1) };

		// Direction vectors for sliding pieces
		private static readonly (int, int)[] RookAttackVectors = { (-1, 0), (1, 0), (0, -1), (0, 1) };

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

		/// <summary>
		///     Checks if a square is attacked by any piece of the specified attacker color.
		///     This implementation is non-recursive to prevent stack overflows during move generation.
		///     It checks for attacks by looking "outward" from the target square for enemy pieces.
		/// </summary>
		/// <param name="square">The square to check.</param>
		/// <param name="attackerColor">The color of the attacking pieces.</param>
		public bool IsSquareAttackedBy(Position square, PieceColor attackerColor)
		{
			// Check for pawn attacks
			if (IsAttackedByPawn(square, attackerColor))
			{
				return true;
			}

			// Check for knight attacks
			if (IsAttackedByPiece(square, attackerColor, PieceType.Knight, KnightAttackVectors))
			{
				return true;
			}

			// Check for king attacks
			if (IsAttackedByPiece(square, attackerColor, PieceType.King, KingAttackVectors))
			{
				return true;
			}

			// Check for sliding attacks (Rooks, Bishops, Queens)
			if (IsAttackedBySlidingPiece(square, attackerColor, RookAttackVectors, PieceType.Rook))
			{
				return true;
			}

			if (IsAttackedBySlidingPiece(square, attackerColor, BishopAttackVectors, PieceType.Bishop))
			{
				return true;
			}

			return false;
		}

		public GameState ExecuteMove(Move move) =>
			MoveExecution.ExecuteMove(this, move);

		public Piece GetPieceAt(Position position) => PiecePositions[position.Row, position.Col];

		/// <summary>
		///     Finds the position of the king for a given color.
		///     Returns null if the king is not on the board.
		/// </summary>
		public Position? FindKingPosition(PieceColor kingColor)
		{
			for (var r = 0 ; r < 8 ; r++)
			{
				for (var c = 0 ; c < 8 ; c++)
				{
					Piece piece = PiecePositions[r, c];
					if (piece.Type == PieceType.King && piece.Color == kingColor)
					{
						return new Position(r, c);
					}
				}
			}

			return null;
		}

		private bool IsAttackedByPawn(Position square, PieceColor attackerColor)
		{
			// Pawns attack diagonally forward. We check from the target square "backwards" 
			// to where an attacking pawn would need to be.
			// White pawns move from high row index to low, Black from low to high.
			int pawnAttackDirection = attackerColor == PieceColor.White ? 1 : -1;
			var leftAttackOrig      = new Position(square.Row + pawnAttackDirection, square.Col - 1);
			var rightAttackOrig     = new Position(square.Row + pawnAttackDirection, square.Col + 1);

			if (BoardHelper.IsInsideBoard(leftAttackOrig))
			{
				Piece piece = GetPieceAt(leftAttackOrig);
				if (piece.Type == PieceType.Pawn && piece.Color == attackerColor)
				{
					return true;
				}
			}

			if (BoardHelper.IsInsideBoard(rightAttackOrig))
			{
				Piece piece = GetPieceAt(rightAttackOrig);
				if (piece.Type == PieceType.Pawn && piece.Color == attackerColor)
				{
					return true;
				}
			}

			return false;
		}

		private bool IsAttackedByPiece(
			Position square, PieceColor attackerColor, PieceType type, (int dRow, int dCol)[] vectors)
		{
			foreach ((int dRow, int dCol) in vectors)
			{
				var potentialAttackerPos = new Position(square.Row + dRow, square.Col + dCol);
				if (!BoardHelper.IsInsideBoard(potentialAttackerPos))
				{
					continue;
				}

				Piece piece = GetPieceAt(potentialAttackerPos);
				if (piece.Type == type && piece.Color == attackerColor)
				{
					return true;
				}
			}

			return false;
		}

		private bool IsAttackedBySlidingPiece(
			Position square, PieceColor attackerColor, (int dRow, int dCol)[] vectors, PieceType sliderType)
		{
			foreach ((int dRow, int dCol) in vectors)
			{
				for (var i = 1 ; i < 8 ; i++)
				{
					var potentialAttackerPos = new Position(square.Row + i * dRow, square.Col + i * dCol);
					if (!BoardHelper.IsInsideBoard(potentialAttackerPos))
					{
						break; // Off board
					}

					Piece piece = GetPieceAt(potentialAttackerPos);
					if (piece.Type != PieceType.None)
					{
						// Found a piece. Is it an attacker?
						if (piece.Color == attackerColor && (piece.Type == sliderType || piece.Type == PieceType.Queen))
						{
							return true;
						}

						// Any piece (friendly or not) blocks further, so stop searching in this direction.
						break;
					}
				}
			}

			return false;
		}
	}
}
