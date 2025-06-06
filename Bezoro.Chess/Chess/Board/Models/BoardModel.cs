using System;
using System.Collections.Generic;
using System.Linq;
using Bezoro.Chess.Chess.Abstractions.Interfaces;
using Bezoro.Chess.Chess.Common.Enums;
using Bezoro.Chess.Chess.Common.Extensions;
using Bezoro.Chess.Chess.Common.Helpers;
using Bezoro.Chess.Chess.Game.Models;
using Bezoro.Chess.Chess.Moves.Models;

namespace Bezoro.Chess.Chess.Board.Models
{
	/// <summary>
	///     Represents the chess board structure and manages piece positions.
	///     Does not manage game rules or overall game state like turns or clocks.
	/// </summary>
	public class BoardModel : IChessBoardModel
	{
		/// <summary>
		///     Initializes a new instance of the chess board model with a specific piece placement.
		/// </summary>
		/// <param name="width">The width of the board.</param>
		/// <param name="height">The height of the board.</param>
		/// <param name="piecePlacementFen">The piece placement part of a FEN string.</param>
		/// <exception cref="ArgumentOutOfRangeException">Thrown when width or height is not positive.</exception>
		/// <exception cref="ArgumentNullException">Thrown if piecePlacementFen is null.</exception>
		public BoardModel(int width, int height, string piecePlacementFen)
		{
			if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width), "Width must be positive.");
			if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive.");
			if (piecePlacementFen == null) throw new ArgumentNullException(nameof(piecePlacementFen));

			Width       = width;
			Height      = height;
			Squares     = InitializeSquares(Width, Height);
			BoardPieces = InitializePieces(Squares, piecePlacementFen);
		}

		private Dictionary<IChessPieceModel, List<Move>> _cachedPseudoLegalMoves = new();

		public Dictionary<IChessPieceModel, BoardPosition> PieceIndex { get; } = new();
		public IChessBoardSquareModel[,]                   Squares    { get; }
		public int                                         Height     { get; }
		public int                                         Width      { get; }
		public IReadOnlyDictionary<IChessPieceModel, List<Move>> CachedPseudoLegalMoves =>
			_cachedPseudoLegalMoves;
		public List<IChessPieceModel> BoardPieces { get; } // Pieces currently on the board

	#region Interface Implementations

		public void SetPieceAt(IChessPieceModel pieceToMove, IChessBoardSquareModel to)
		{
			if (pieceToMove == null) throw new ArgumentNullException(nameof(pieceToMove));
			if (to          == null) throw new ArgumentNullException(nameof(to));

			// Remove from old position if it was already on the board
			if (PieceIndex.TryGetValue(pieceToMove, out var oldPos))
			{
				var oldSquare = this.GetSquareAt(oldPos);
				if (oldSquare.GetPiece() == pieceToMove) // Ensure it's the same piece instance
				{
					oldSquare.SetPiece(null);
				}
			}

			// Remove from BoardPieces list if it was already there to avoid duplicates
			if (BoardPieces.Contains(pieceToMove))
			{
				BoardPieces.Remove(pieceToMove);
			}

			// If there's a piece at the target square, it's considered "off-board" by this action.
			// Captures are handled by GameModel logic before calling low-level move methods.
			var existingPieceAtTarget = to.GetPiece();
			if (existingPieceAtTarget != null)
			{
				PieceIndex.Remove(existingPieceAtTarget);
				BoardPieces.Remove(existingPieceAtTarget);
			}

			to.SetPiece(pieceToMove);
			UpdateIndex(pieceToMove, to.Position);
			if (!BoardPieces.Contains(pieceToMove)) // Add if not already present
			{
				BoardPieces.Add(pieceToMove);
			}
		}

		public void Clear()
		{
			foreach (var square in Squares)
			{
				square.SetPiece(null);
			}
		}

		public bool IsEnemy(IChessBoardSquareModel targetSquare, PlayerColor myColor)
		{
			if (targetSquare == null) throw new ArgumentNullException(nameof(targetSquare));

			var targetPiece = targetSquare.GetPiece();
			if (targetPiece == null)
				return false;

			return targetPiece.Color != myColor;
		}

		public bool IsEmpty(BoardPosition to) => this.GetPieceAt(to) == null;

		public BoardPosition? GetPosition(IChessPieceModel piece) =>
			PieceIndex.TryGetValue(piece, out var pos) ? pos : null;

		public IEnumerable<IChessBoardSquareModel> GetStraightPath(BoardPosition from, BoardPosition to)
		{
			if (from == null) throw new ArgumentNullException(nameof(from));
			if (to   == null) throw new ArgumentNullException(nameof(to));

			var dx = Math.Sign(to.File - from.File);
			var dy = Math.Sign(to.Rank - from.Rank);

			if (dx == 0 && dy == 0)
				throw new InvalidOperationException("Source and target squares are identical.");

			if (dx != 0 && dy != 0)
				throw new InvalidOperationException("Path must be horizontal or vertical.");

			var curFile = from.File + dx;
			var curRank = from.Rank + dy;

			while (curFile != to.File || curRank != to.Rank)
			{
				yield return Squares[curFile, curRank];
				curFile += dx;
				curRank += dy;
			}
		}

		public void MovePiece(IChessPieceModel piece, string fromAlgebraic, string toAlgebraic)
		{
			var from = AlgebraicNotationUtils.FromAlgebraic(fromAlgebraic);
			var to   = AlgebraicNotationUtils.FromAlgebraic(toAlgebraic);
			MovePieceInternal(piece, from, to);
		}

		public void MovePiece(IChessPieceModel piece, BoardPosition from, BoardPosition to)
			=> MovePieceInternal(piece, from, to);

		public bool IsSquareAttacked(BoardPosition position, PlayerColor attackerColor)
		{
			if (position == null)
				throw new ArgumentNullException(nameof(position));

			throw new NotImplementedException();
		}

		/// <summary>
		///     Retrieves the cached moves for a specific piece.
		///     Returns an empty collection if the piece is unknown or no cache is present.
		/// </summary>
		public IReadOnlyList<Move> GetCachedMovesFor(IChessPieceModel piece)
		{
			if (piece == null) throw new ArgumentNullException(nameof(piece));

			return _cachedPseudoLegalMoves.TryGetValue(piece, out var moves)
				? moves
				: Array.Empty<Move>();
		}

		/// <summary>
		///     Rebuilds the cache that maps every on-board piece to the full set of its
		///     current pseudo-legal moves.
		///     This cache can later be consulted (e.g. when verifying that the king is not
		///     placed in check by a prospective move).
		/// </summary>
		/// <param name="game">
		///     The <see cref="GameModel" /> instance providing the necessary context to each
		///     piece’s <see cref="IChessPieceModel.GetPseudoLegalMoves" /> implementation.
		/// </param>
		/// <exception cref="ArgumentNullException">
		///     Thrown if <paramref name="game" /> is <c>null</c>.
		/// </exception>
		public void RefreshPseudoLegalMoveCache(GameModel game)
		{
			if (game == null) throw new ArgumentNullException(nameof(game));

			_cachedPseudoLegalMoves = new(BoardPieces.Count);

			foreach (var piece in BoardPieces)
			{
				var moves = piece.GetPseudoLegalMoves(game);
				_cachedPseudoLegalMoves[piece] = moves.ToList();
			}
		}

	#endregion

		/// <summary>
		///     Internal method to move a piece on the board. This method handles updating piece lists
		///     and indices but does not manage captures or game state rules like clocks or turns.
		///     It assumes the move is physically possible for the board layout.
		///     GameModel is responsible for higher-level validation and state updates.
		/// </summary>
		internal void MovePieceInternal(IChessPieceModel pieceToMove, BoardPosition from, BoardPosition to)
		{
			if (pieceToMove == null) throw new ArgumentNullException(nameof(pieceToMove));
			if (!this.IsInside(from)) throw new InvalidOperationException($"Source position {from} is out of bounds.");
			if (!this.IsInside(to)) throw new InvalidOperationException($"Target position {to} is out of bounds.");

			var fromSquare = this.GetSquareAt(from);
			var toSquare   = this.GetSquareAt(to);

			if (fromSquare.GetPiece() != pieceToMove)
			{
				// Attempt to find the piece if _pieceIndex is out of sync or if called directly
				if (PieceIndex.TryGetValue(pieceToMove, out var actualPos) && actualPos == from)
				{
					// _pieceIndex is correct, but square might not be. This indicates an inconsistency.
					// For now, proceed if _pieceIndex matches 'from'.
				}
				else
				{
					throw new InvalidOperationException(
						$"Piece {pieceToMove} is not at the source position {from}. Recorded at: {(PieceIndex.TryGetValue(pieceToMove, out var p) ? p.Algebraic : "N/A")}");
				}
			}

			// Handle the piece at the target square.
			// GameModel should have already determined if this is a capture and added to CapturedPieces list.
			// BoardModel's role here is to remove it from its own tracking if it exists.
			var pieceAtTarget = toSquare.GetPiece();
			if (pieceAtTarget != null)
			{
				if (pieceAtTarget == pieceToMove)
				{
					// Trying to move to its own square is generally not allowed unless it's a null move,
					// which should be handled by GameModel.
					// For BoardModel, if from == to, we essentially do nothing here but update index if needed.
					if (from.Equals(to))
					{
						PieceIndex[pieceToMove] = to; // Ensure index is current
						return;
					}
					// If it's a different piece, it means GameModel didn't handle its removal for a capture.
					// Or, it's an overwrite, which this simple model allows by removing.
				}

				PieceIndex.Remove(pieceAtTarget);
				BoardPieces.Remove(pieceAtTarget); // Remove from active board pieces
			}

			fromSquare.SetPiece(null);
			toSquare.SetPiece(pieceToMove);
			PieceIndex[pieceToMove] = to; // Update the index for the moved piece
		}

		internal void UpdateIndex(IChessPieceModel piece, BoardPosition newPos) => PieceIndex[piece] = newPos;

		private BoardSnapshot CreateSnapshot() => new(Squares); // Pass necessary data to snapshot

		private static IChessBoardSquareModel[,] InitializeSquares(int width, int height)
		{
			var squares = new IChessBoardSquareModel[width, height];
			for (var file = 0 ; file < width ; file++)
			{
				for (var rank = 0 ; rank < height ; rank++)
				{
					squares[file, rank] = new BoardSquareModel(file, rank);
				}
			}

			return squares;
		}

		private List<IChessPieceModel> InitializePieces(
			IChessBoardSquareModel[,] boardSquares,
			string piecePlacementFen)
		{
			var pieceList = new List<IChessPieceModel>();
			var rank      = Height - 1; // FEN starts from 8th rank
			var file      = 0;          // FEN starts from a-file

			foreach (var symbol in piecePlacementFen)
			{
				if (symbol == '/')
				{
					rank--;
					file = 0;
				}
				else if (char.IsDigit(symbol))
				{
					file += int.Parse(symbol.ToString());
				}
				else
				{
					// Assuming ChessUtils.GetPieceFromChar exists and returns IChessPieceModel
					var piece  = ChessUtils.GetPieceFromChar(symbol);
					var square = boardSquares[file, rank];
					square.SetPiece(piece);
					pieceList.Add(piece);
					UpdateIndex(piece, new(file, rank));
					file++;
				}
			}

			return pieceList;
		}
	}
}
