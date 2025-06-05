using System;
using System.Collections.Generic;
using Bezoro.Core.Chess.Abstractions.Interfaces;
using Bezoro.Core.Chess.Board.Models;
using Bezoro.Core.Chess.Common.Enums;
using Bezoro.Core.Chess.Common.Extensions;
using Bezoro.Core.Chess.Common.Helpers;
using Bezoro.Core.Chess.Moves.Models;
using Bezoro.Core.Chess.Pieces.Models;

namespace Bezoro.Core.Chess.Board
{
	/// <summary>
	///     Immutable representation of a board position that can be cloned and
	///     queried without touching the live <see cref="BoardModel" /> instance.
	/// </summary>
	public sealed class BoardSnapshot
	{
		/*──────────────────────────────────────────────────────────*/
		/*  Construction / cloning                                  */
		/*──────────────────────────────────────────────────────────*/

		internal BoardSnapshot(IChessBoardSquareModel[,] piecesSource)
		{
			Width  = piecesSource.GetLength(0);
			Height = piecesSource.GetLength(1);

			_pieces = new IChessPieceModel?[Width, Height];
			// Assuming IChessBoardSquareModel might be directly an IChessPieceModel
			// or has a property to get the IChessPieceModel.
			// If IChessBoardSquareModel is not IChessPieceModel, this copy needs adjustment.
			// For example, if it's a wrapper:
			// for (int i = 0; i < Width; i++)
			// {
			//     for (int j = 0; j < Height; j++)
			//     {
			//         _pieces[i, j] = piecesSource[i, j]?.Piece; // Assuming a 'Piece' property
			//     }
			// }
			// Given the original Array.Copy, it implies piecesSource elements are compatible with _pieces elements.
			// Let's stick to the original interpretation for now, assuming compatibility.
			if (piecesSource is IChessPieceModel?[,] pieceModels)
			{
				Array.Copy(pieceModels, _pieces, pieceModels.Length);
			}
			else if (typeof(IChessBoardSquareModel)
					 == typeof(IChessPieceModel)) // If they are effectively the same type
			{
				Array.Copy(piecesSource, _pieces, piecesSource.Length);
			}
			else
			{
				// Fallback if direct copy is not possible and IChessBoardSquareModel is more complex
				// This part would need to know how to extract IChessPieceModel from IChessBoardSquareModel
				for (var i = 0 ; i < Width ; i++)
				{
					for (var j = 0 ; j < Height ; j++)
					{
						_pieces[i, j] =
							piecesSource[i,
								j] as IChessPieceModel; // Simplistic cast, might need specific adapter logic
					}
				}
			}
		}

		private BoardSnapshot(IChessPieceModel?[,] pieceModels, int width, int height)
		{
			Width   = width;
			Height  = height;
			_pieces = new IChessPieceModel?[width, height];
			Array.Copy(pieceModels, _pieces, pieceModels.Length);
		}

		/*──────────────────────────────────────────────────────────*/
		/*  Indexer                                                 */
		/*──────────────────────────────────────────────────────────*/

		public IChessPieceModel? this[int file, int rank]
		{
			get
			{
				if (!_pieces.IsInside(file, rank))
				{
					throw new ArgumentOutOfRangeException(
						$"Coordinates ({file},{rank}) outside board bounds.");
				}

				return _pieces[file, rank];
			}
			private set
			{
				if (!_pieces.IsInside(file, rank))
				{
					throw new ArgumentOutOfRangeException(
						$"Coordinates ({file},{rank}) outside board bounds.");
				}

				_pieces[file, rank] = value;
			}
		}
		private readonly IChessPieceModel?[,] _pieces;
		public           int                  Height { get; }

		public int Width { get; }

		public BoardPosition? FindKing(PlayerColor side)
		{
			foreach (var (piece, pos) in EnumeratePieces())
			{
				if (GetPieceType(piece) == ChessPieceType.King && piece.Color == side)
					return pos;
			}

			return null;
		}

		public BoardSnapshot ApplyMove(Move move)
		{
			if (move == null) throw new ArgumentNullException(nameof(move));

			// Create a new internal representation for the next state by deep copying the current _pieces array.
			var newPiecesArray = new IChessPieceModel?[Width, Height];
			Array.Copy(_pieces, newPiecesArray, _pieces.Length);
			var next = new BoardSnapshot(newPiecesArray, Width, Height); // Use private constructor for true clone

			var movingPiece = next[move.From.File, move.From.Rank];
			if (movingPiece == null)
			{
				throw new InvalidOperationException($"No piece at {move.From} to apply move.");
			}

			var movingPieceColor = movingPiece.Color;
			var movingPieceType  = movingPiece.GetPieceType();

			var currentHasMoved = false;
			if (movingPiece is PieceModel pieceModel)
			{
				currentHasMoved = pieceModel.HasMoved;
			}
			// If KingModel/RookModel don't derive from a common PieceModel with HasMoved,
			// you might need more specific checks here, e.g.:
			// else if (movingPiece is KingModel km) currentHasMoved = km.HasMoved;
			// else if (movingPiece is RookModel rm) currentHasMoved = rm.HasMoved;

			// Clear the original square
			next[move.From.File, move.From.Rank] = null;

			IChessPieceModel pieceToPlace;

			switch (move.Kind)
			{
				case MoveKind.Normal:
					var newHasMovedNormal = currentHasMoved
											|| movingPieceType == ChessPieceType.King
											|| movingPieceType == ChessPieceType.Rook;

					pieceToPlace = CreateUpdatedPiece(movingPieceColor, movingPieceType, newHasMovedNormal);

					next[move.To.File, move.To.Rank] = pieceToPlace;
					break;

				case MoveKind.Promotion:
					if (!move.PromoteTo.HasValue)
					{
						throw new InvalidOperationException("Promotion move must specify promotion piece type.");
					}

					ChessPieceType promotedPieceType;
					switch (move.PromoteTo.Value)
					{
						case PromotionPieceType.Queen:
							promotedPieceType = ChessPieceType.Queen;
							break;
						case PromotionPieceType.Rook:
							promotedPieceType = ChessPieceType.Rook;
							break;
						case PromotionPieceType.Bishop:
							promotedPieceType = ChessPieceType.Bishop;
							break;
						case PromotionPieceType.Knight:
							promotedPieceType = ChessPieceType.Knight;
							break;
						default:
							throw new ArgumentOutOfRangeException(
								nameof(move.PromoteTo), "Invalid promotion piece type.");
					}

					// A promoted piece should always be considered "moved", especially if it's a Rook.
					pieceToPlace                     = CreateUpdatedPiece(movingPieceColor, promotedPieceType, true);
					next[move.To.File, move.To.Rank] = pieceToPlace;
					break;

				case MoveKind.EnPassant:
					// The pawn itself moves.
					// 'false' for hasMoved as pawn's initial two-step capability isn't what this flag is for (castling).
					pieceToPlace = CreateUpdatedPiece(movingPieceColor, movingPieceType, false);

					next[move.To.File, move.To.Rank] = pieceToPlace;
					// Remove the captured pawn in en passant
					var capturedPawnPos = new BoardPosition(move.To.File, move.From.Rank);
					next[capturedPawnPos.File, capturedPawnPos.Rank] = null;
					break;

				case MoveKind.CastleKingside:
				case MoveKind.CastleQueenside:
					// King moves and its HasMoved status is set to true.
					var movedKing = CreateUpdatedPiece(movingPieceColor, ChessPieceType.King, true);
					next[move.To.File, move.To.Rank] = movedKing;

					// Determine Rook's movement
					var           rank = move.From.Rank;
					BoardPosition rookFromPos, rookToPos;
					if (move.Kind == MoveKind.CastleKingside)
					{
						rookFromPos = new(next.Width   - 1, rank); // Assuming H-file is Width-1
						rookToPos   = new(move.To.File - 1, rank); // King moves two squares, rook to adjacent square
					}
					else // CastleQueenside
					{
						rookFromPos = new(0, rank);                // Assuming A-file is 0
						rookToPos   = new(move.To.File + 1, rank); // King moves two squares, rook to adjacent square
					}

					var rook = next[rookFromPos.File, rookFromPos.Rank];
					if (rook == null || rook.GetPieceType() != ChessPieceType.Rook)
					{
						throw new InvalidOperationException(
							$"Rook not found at {rookFromPos} or not a rook for castling.");
					}

					var movedRook = CreateUpdatedPiece(rook.Color, ChessPieceType.Rook, true);
					next[rookFromPos.File, rookFromPos.Rank] = null;
					next[rookToPos.File, rookToPos.Rank]     = movedRook;
					break;

				default:
					throw new ArgumentOutOfRangeException(nameof(move.Kind), $"Unknown move kind: {move.Kind}");
			}

			return next;
		}

		public BoardSnapshot Clone()
		{
			// Ensure a deep copy of the _pieces array for the new snapshot
			var newPiecesArray = new IChessPieceModel?[Width, Height];
			Array.Copy(_pieces, newPiecesArray, _pieces.Length);
			return new(newPiecesArray, Width, Height); // Use the private constructor
		}

		/*──────────────────────────────────────────────────────────*/
		/*  Attack detection                                        */
		/*──────────────────────────────────────────────────────────*/

		public bool IsSquareAttacked(BoardPosition target, PlayerColor by)
		{
			var (tf, tr) = (target.File, target.Rank);

			/* -------- Knights ----------------------------------------- */
			foreach (var (df, dr) in DirectionVectors.Knight)
			{
				if (_pieces.TryGetPiece(tf + df, tr + dr, out var pN)
					&& pN!.Color         == by
					&& pN.GetPieceType() == ChessPieceType.Knight)
					return true;
			}

			/* -------- Pawns (capture vectors only) -------------------- */
			var attackingPawnRankOffset = by == PlayerColor.White ? -1 : +1;
			var attackingPawnRank       = tr + attackingPawnRankOffset;

			foreach (var df in new[] { -1, +1 })
			{
				if (TryGetPiece(tf + df, attackingPawnRank, out var pP)
					&& pP!.Color         == by
					&& pP.GetPieceType() == ChessPieceType.Pawn)
					return true;
			}

			/* -------- King (adjacent) --------------------------------- */
			foreach (var (df, dr) in DirectionVectors.King)
			{
				if (TryGetPiece(tf + df, tr + dr, out var pK)
					&& pK!.Color         == by
					&& pK.GetPieceType() == ChessPieceType.King)
					return true;
			}

			/* -------- Sliders: rook / bishop / queen ------------------ */

			// Orthogonal rays – rook or queen
			foreach (var (df, dr) in DirectionVectors.Orthogonal)
			{
				if (RayHitsSlider(tf, tr, df, dr, by, false))
					return true;
			}

			// Diagonal rays – bishop or queen
			foreach (var (df, dr) in DirectionVectors.Diagonal)
			{
				if (RayHitsSlider(tf, tr, df, dr, by, true))
					return true;
			}

			return false;
		}

		/*──────────────────────────────────────────────────────────*/
		/*  Basic enumeration utilities                             */
		/*──────────────────────────────────────────────────────────*/

		public IEnumerable<(IChessPieceModel piece, BoardPosition pos)> EnumeratePieces()
		{
			for (var f = 0 ; f < Width ; ++f)
			{
				for (var r = 0 ; r < Height ; ++r)
				{
					if (_pieces[f, r] is { } p)
						yield return (p, new(f, r));
				}
			}
		}

		private bool RayHitsSlider(
			int fStart,
			int rStart,
			int df,
			int dr,
			PlayerColor attacker,
			bool diag)
		{
			foreach (var (nf, nr) in _pieces.Ray(fStart, rStart, df, dr))
			{
				var piece = _pieces[nf, nr];

				// Empty square – keep going.
				if (piece is null)
					continue;

				// We hit a piece; see if it is an attacking slider.
				if (piece.Color == attacker)
				{
					var type = piece.GetPieceType();
					if (type == ChessPieceType.Queen) return true;
					if (diag  && type == ChessPieceType.Bishop) return true;
					if (!diag && type == ChessPieceType.Rook) return true;
				}

				// Either friendly blocker or wrong piece type – stop the ray.
				break;
			}

			return false;
		}

		/*──────────────────────────────────────────────────────────*/
		/*  Helpers                                                 */
		/*──────────────────────────────────────────────────────────*/

		private bool TryGetPiece(int f, int r, out IChessPieceModel? piece) =>
			_pieces.TryGetPiece(f, r, out piece);

		private static ChessPieceType GetPieceType(IChessPieceModel piece) =>
			piece.GetPieceType();

		// Helper to create a new piece instance, primarily for updating HasMoved state or for promotion.
		private IChessPieceModel CreateUpdatedPiece(
			PlayerColor color,
			ChessPieceType type,
			bool hasMoved)
		{
			// Assumes piece classes (e.g., KingModel, RookModel) have a constructor PlayerColor color
			// and a MarkMoved() method or a similar mechanism to set HasMoved.
			switch (type)
			{
				case ChessPieceType.King:
					var king = new KingModel(color);
					if (hasMoved) king.MarkMoved();
					return king;
				case ChessPieceType.Rook:
					var rook = new RookModel(color);
					if (hasMoved) rook.MarkMoved();
					return rook;
				case ChessPieceType.Queen:
					var queen = new QueenModel(color);
					if (hasMoved) queen.MarkMoved();
					return queen;
				case ChessPieceType.Bishop:
					var bishop = new BishopModel(color);
					if (hasMoved) bishop.MarkMoved();
					return bishop;
				case ChessPieceType.Knight:
					var knight = new KnightModel(color);
					if (hasMoved) knight.MarkMoved();
					return knight;
				case ChessPieceType.Pawn:
					return new PawnModel(color);
				default:
					throw new ArgumentOutOfRangeException(nameof(type), $"Unsupported piece type for creation: {type}");
			}
		}
	}
}
