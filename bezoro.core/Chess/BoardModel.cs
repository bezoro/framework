using System;
using System.Collections.Generic;
using System.Text;
using Bezoro.Core.Chess.Interfaces;
using Bezoro.Core.Chess.Utils;

// For AlgebraicNotationUtils, ChessUtils

namespace Bezoro.Core.Chess
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
			// Note: CapturedPieces list is now managed by GameModel
		}

		private BoardSnapshot? _cachedSnapshot;
		private bool           _snapshotValid;

		private readonly Dictionary<IChessPieceModel, BoardPosition> _pieceIndex = new();

		public BoardSnapshot Snapshot
		{
			get
			{
				if (_snapshotValid && _cachedSnapshot is not null)
					return _cachedSnapshot;

				_cachedSnapshot = CreateSnapshot();
				_snapshotValid  = true;
				return _cachedSnapshot;
			}
		}

		public IChessBoardSquareModel[,] Squares     { get; }
		public int                       Height      { get; }
		public int                       Width       { get; }
		public List<IChessPieceModel>    BoardPieces { get; } // Pieces currently on the board

	#region Interface Implementations

		public void SetPieceAt(IChessPieceModel pieceToMove, IChessBoardSquareModel to)
		{
			if (pieceToMove == null) throw new ArgumentNullException(nameof(pieceToMove));
			if (to          == null) throw new ArgumentNullException(nameof(to));

			// Remove from old position if it was already on the board
			if (_pieceIndex.TryGetValue(pieceToMove, out var oldPos))
			{
				var oldSquare = GetSquare(oldPos);
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
				_pieceIndex.Remove(existingPieceAtTarget);
				BoardPieces.Remove(existingPieceAtTarget);
			}

			to.SetPiece(pieceToMove);
			UpdateIndex(pieceToMove, to.Position);
			if (!BoardPieces.Contains(pieceToMove)) // Add if not already present
			{
				BoardPieces.Add(pieceToMove);
			}

			InvalidateSnapshot();
		}

		public IChessBoardSquareModel GetSquare(BoardPosition position)
		{
			if (!IsValid(position))
				throw new ArgumentOutOfRangeException(nameof(position), "Position is out of bounds.");

			return Squares[position.Column, position.Row];
		}

		public bool IsEmpty(BoardPosition to) => GetPieceAt(to) == null;

		public BoardPosition? GetPosition(IChessPieceModel piece) =>
			_pieceIndex.TryGetValue(piece, out var pos) ? pos : null;

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

	#endregion

		public bool IsSquareAttacked(BoardPosition position, PlayerColor attackerColor)
		{
			if (position == null) throw new ArgumentNullException(nameof(position));
			return Snapshot.IsSquareAttacked(position, attackerColor);
		}

		public IChessBoardSquareModel GetSquare(string algebraicPos)
		{
			var pos = AlgebraicNotationUtils.FromAlgebraic(algebraicPos);
			if (!IsValid(pos))
				throw new ArgumentOutOfRangeException(nameof(algebraicPos), "Position is out of bounds.");

			return Squares[pos.Column, pos.Row];
		}

		public IChessPieceModel? GetPieceAt(BoardPosition position)
		{
			if (!IsValid(position)) return null;
			return Squares[position.Column, position.Row].GetPiece();
		}

		public IChessPieceModel? GetPieceAt(string algebraicPos)
		{
			var pos = AlgebraicNotationUtils.FromAlgebraic(algebraicPos);
			if (!IsValid(pos)) return null;
			return Squares[pos.Column, pos.Row].GetPiece();
		}

		/// <summary>
		///     Generates the piece placement part of the Forsyth-Edwards Notation (FEN) string.
		/// </summary>
		/// <returns>The FEN piece placement string.</returns>
		public string GetPiecePlacementFen()
		{
			var fenPart = new StringBuilder();
			for (var rank = Height - 1 ; rank >= 0 ; rank--)
			{
				var emptySquares = 0;
				for (var file = 0 ; file < Width ; file++)
				{
					var piece = GetPieceAt(new BoardPosition(file, rank));
					if (piece == null)
					{
						emptySquares++;
					}
					else
					{
						if (emptySquares > 0)
						{
							fenPart.Append(emptySquares);
							emptySquares = 0;
						}

						// Assuming ChessUtils.GetCharFromPiece exists and IChessPieceModel is compatible
						fenPart.Append(ChessUtils.GetCharFromPiece(piece));
					}
				}

				if (emptySquares > 0)
				{
					fenPart.Append(emptySquares);
				}

				if (rank > 0)
				{
					fenPart.Append('/');
				}
			}

			return fenPart.ToString();
		}

		internal bool IsValid(BoardPosition position) =>
			position.Column >= 0 && position.Column < Width && position.Row >= 0 && position.Row < Height;

		internal void InvalidateSnapshot() => _snapshotValid = false;

		/// <summary>
		///     Internal method to move a piece on the board. This method handles updating piece lists
		///     and indices but does not manage captures or game state rules like clocks or turns.
		///     It assumes the move is physically possible for the board layout.
		///     GameModel is responsible for higher-level validation and state updates.
		/// </summary>
		internal void MovePieceInternal(IChessPieceModel pieceToMove, BoardPosition from, BoardPosition to)
		{
			if (pieceToMove == null) throw new ArgumentNullException(nameof(pieceToMove));
			if (!IsValid(from)) throw new InvalidOperationException($"Source position {from} is out of bounds.");
			if (!IsValid(to)) throw new InvalidOperationException($"Target position {to} is out of bounds.");

			var fromSquare = GetSquare(from);
			var toSquare   = GetSquare(to);

			if (fromSquare.GetPiece() != pieceToMove)
			{
				// Attempt to find the piece if _pieceIndex is out of sync or if called directly
				if (_pieceIndex.TryGetValue(pieceToMove, out var actualPos) && actualPos == from)
				{
					// _pieceIndex is correct, but square might not be. This indicates an inconsistency.
					// For now, proceed if _pieceIndex matches 'from'.
				}
				else
				{
					throw new InvalidOperationException(
						$"Piece {pieceToMove} is not at the source position {from}. Recorded at: {(_pieceIndex.TryGetValue(pieceToMove, out var p) ? p.Algebraic : "N/A")}");
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
						_pieceIndex[pieceToMove] = to; // Ensure index is current
						InvalidateSnapshot();
						return;
					}
					// If it's a different piece, it means GameModel didn't handle its removal for a capture.
					// Or, it's an overwrite, which this simple model allows by removing.
				}

				_pieceIndex.Remove(pieceAtTarget);
				BoardPieces.Remove(pieceAtTarget); // Remove from active board pieces
			}

			fromSquare.SetPiece(null);
			toSquare.SetPiece(pieceToMove);
			_pieceIndex[pieceToMove] = to; // Update the index for the moved piece

			InvalidateSnapshot();
		}

		internal void UpdateIndex(IChessPieceModel piece, BoardPosition newPos) => _pieceIndex[piece] = newPos;

		private BoardSnapshot CreateSnapshot() => new(Squares); // Pass necessary data to snapshot

		private static IChessBoardSquareModel[,] InitializeSquares(int width, int height)
		{
			var squares = new IChessBoardSquareModel[width, height];
			for (var file = 0 ; file < width ; file++)
			{
				for (var rank = 0 ; rank < height ; rank++)
				{
					squares[file, rank] = new BoardSquareModel(new(file, rank));
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
