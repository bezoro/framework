using Bezoro.Chess.Domain.Functions.Moves;
using Bezoro.Chess.Domain.Helpers;
using Bezoro.Chess.Domain.Shared.Consts;
using Bezoro.Chess.Domain.Shared.Enums;
using Bezoro.Chess.Domain.Types.Structs;

namespace Bezoro.Chess.Domain.Types.Records
{
	internal record GameState
	{
		// ── New: the board is now backed by bitboards ────────────────
		public Board Board { get; init; } = new(BoardFactory.CreateInitialBitboards());

		/// <summary>
		///     A bitmask representing the current castling availability.
		/// </summary>
		public CastlingRights Castling { get; init; } = CastlingRights.All;

		/// <summary>
		///     The color of the player whose turn it is to move.
		/// </summary>
		public PieceColor ActiveColor { get; init; } = PieceColor.White;

		/// <summary>
		///     If a pawn has just made a two-square move, this is the position "behind" the pawn.
		///     This is needed for en passant capture validation.
		/// </summary>
		public Position EnPassantTargetSquare { get; init; }

		/// <summary>
		///     The number of full moves in the game. Starts at 1 and increments after Black moves.
		/// </summary>
		public uint FullMoveNumber { get; init; } = 1;

		/// <summary>
		///     The number of half-moves since the last capture or pawn advance.
		///     Used for the fifty-move rule.
		/// </summary>
		public uint HalfMoveClock { get; init; }

		/// <summary>
		///     Creates an initial <see cref="GameState" /> that represents the
		///     standard chess starting position.
		/// </summary>
		/// <param name="activeColor">
		///     Which side is to make the first move – defaults to White but may be
		///     set to Black for testing or special game modes.
		/// </param>
		public static GameState CreateInitial(PieceColor activeColor = PieceColor.White) =>
			new()
			{
				Board                 = new Board(BoardFactory.CreateInitialBitboards()),
				Castling              = CastlingRights.All,
				FullMoveNumber        = 1,
				HalfMoveClock         = 0,
				ActiveColor           = activeColor,
				EnPassantTargetSquare = default
			};

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
			if (IsAttackedByPiece(square, attackerColor, PieceType.Knight, AttackVectors.KnightAttackVectors))
			{
				return true;
			}

			// Check for king attacks
			if (IsAttackedByPiece(square, attackerColor, PieceType.King, AttackVectors.KingAttackVectors))
			{
				return true;
			}

			// Check for sliding attacks (Rooks, Bishops, Queens)
			if (IsAttackedBySlidingPiece(square, attackerColor, AttackVectors.RookAttackVectors, PieceType.Rook))
			{
				return true;
			}

			if (IsAttackedBySlidingPiece(square, attackerColor, AttackVectors.BishopAttackVectors, PieceType.Bishop))
			{
				return true;
			}

			return false;
		}

		public GameState ExecuteMove(Move move) =>
			MoveExecution.ExecuteMove(this, move);

		public Piece GetPieceAt(Position p) => Board.GetPiece(p);

		/// <summary>
		///     Finds the position of the king for a given color.
		///     Returns null if the king is not on the board.
		/// </summary>
		public Position FindKingPosition(PieceColor kingColor)
		{
			for (var r = 0 ; r < 8 ; r++)
			{
				for (var c = 0 ; c < 8 ; c++)
				{
					// ask the board for the piece on this square
					Piece piece = Board.GetPiece(new Position(r, c));
					if (piece.Type == PieceType.King && piece.Color == kingColor)
					{
						return new Position(r, c);
					}
				}
			}

			return default;
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
