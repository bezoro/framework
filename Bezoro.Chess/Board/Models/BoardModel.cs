using System;
using System.Collections.Generic;
using System.Linq;
using Bezoro.Chess.Abstractions.Interfaces;
using Bezoro.Chess.Common.Enums;
using Bezoro.Chess.Common.Helpers;
using Bezoro.Chess.Game.Models;
using Bezoro.Chess.Moves.Models;

namespace Bezoro.Chess.Board.Models
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
		/// <param name="enPassant">The en passant target square in algebraic notation (e.g., "e3") or "-" if none.</param>
		/// <exception cref="ArgumentOutOfRangeException">Thrown when width or height is not positive.</exception>
		/// <exception cref="ArgumentNullException">Thrown if piecePlacementFen is null.</exception>
		public BoardModel(int width = 8, int height = 8, string? piecePlacementFen = null, string enPassant = "-")
		{
			if (width  <= 0) throw new ArgumentOutOfRangeException(nameof(width),  "Width must be positive.");
			if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive.");

			piecePlacementFen ??= FenUtils.StartPieces;

			Width                 = width;
			Height                = height;
			Squares               = InitializeSquares(Width, Height);
			BoardPieces           = InitializePieces(Squares, piecePlacementFen);
			EnPassantTargetSquare = ResolveEnPassantTarget(enPassant);
		}

		private Dictionary<IChessPieceModel, List<Move>> _cachedPseudoLegalMoves = new();

		public Dictionary<IChessPieceModel, BoardPosition> PieceIndex { get; } = new();
		public IChessBoardSquareModel[,]                   Squares    { get; }
		public int                                         Height     { get; }
		public int                                         Width      { get; }
		public IReadOnlyDictionary<IChessPieceModel, List<Move>> CachedPseudoLegalMoves =>
			_cachedPseudoLegalMoves;
		public List<IChessPieceModel> BoardPieces { get; } // Pieces currently on the board

		public IChessBoardSquareModel? EnPassantTargetSquare { get; private set; }

	#region Interface Implementations

		public void SetPieceAt(IChessPieceModel pieceToMove, IChessBoardSquareModel to)
		{
			if (pieceToMove == null) throw new ArgumentNullException(nameof(pieceToMove));
			if (to          == null) throw new ArgumentNullException(nameof(to));

			// Remove from old position if it was already on the board
			if (PieceIndex.TryGetValue(pieceToMove, out var oldPos))
			{
				var oldSquare = GetSquareAt(oldPos);      // Assuming GetSquareAt extension method
				if (oldSquare?.GetPiece() == pieceToMove) // Ensure it's the same piece instance
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

		public IChessBoardModel Clear()
		{
			foreach (var square in Squares)
			{
				square.SetPiece(null);
			}

			PieceIndex.Clear();
			BoardPieces.Clear();             // Also clear pieces list
			EnPassantTargetSquare = null;    // Clear en passant target
			_cachedPseudoLegalMoves.Clear(); // Clear cache
			return this;
		}

		public bool IsEnemy(IChessBoardSquareModel targetSquare, PlayerColor myColor)
		{
			if (targetSquare == null) throw new ArgumentNullException(nameof(targetSquare));

			var targetPiece = targetSquare.GetPiece();
			if (targetPiece == null)
				return false;

			return targetPiece.Color != myColor;
		}

		public bool IsEmpty(BoardPosition to) => GetSquareAt(to)?.GetPiece() == null;

		public BoardPosition? GetPosition(IChessPieceModel piece) =>
			PieceIndex.TryGetValue(piece, out var pos) ? pos : null;

		public IEnumerable<IChessBoardSquareModel> GetStraightPath(BoardPosition from, BoardPosition to)
		{
			if (from == null) throw new ArgumentNullException(nameof(from));
			if (to   == null) throw new ArgumentNullException(nameof(to));

			var dx = Math.Sign(to.Column - from.Column);
			var dy = Math.Sign(to.Row    - from.Row);

			if (dx == 0 && dy == 0)
				throw new InvalidOperationException("Source and target squares are identical.");

			if (dx                                != 0 &&
				dy                                != 0 &&
				Math.Abs(to.Column - from.Column) != Math.Abs(to.Rank - from.Rank)) // Allow diagonal
				throw new InvalidOperationException("Path must be horizontal, vertical, or purely diagonal.");

			var currentCol  = from.Column + dx;
			var currentRank = from.Rank   + dy;

			while (currentCol != to.Column || currentRank != to.Rank)
			{
				if (currentCol < 0 || currentCol >= Width || currentRank < 0 || currentRank >= Height)
					throw new InvalidOperationException("Path goes out of bounds.");

				yield return Squares[currentCol, currentRank];
				currentCol  += dx;
				currentRank += dy;
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

			// This requires a GameModel context to get pseudo-legal moves for opponent pieces
			throw new NotImplementedException(
				"IsSquareAttacked needs GameModel context or direct attack generation logic.");
		}

		public IReadOnlyList<Move> GetCachedMovesFor(IChessPieceModel piece)
		{
			if (piece == null) throw new ArgumentNullException(nameof(piece));

			return _cachedPseudoLegalMoves.TryGetValue(piece, out var moves)
				? moves
				: Array.Empty<Move>();
		}

		public void RefreshPseudoLegalMoveCache(GameModel game)
		{
			if (game == null) throw new ArgumentNullException(nameof(game));

			_cachedPseudoLegalMoves = new(BoardPieces.Count);

			foreach (var piece in BoardPieces)
			{
				var moves = piece.GetPseudoLegalMoves(game); // GameModel context is crucial here
				_cachedPseudoLegalMoves[piece] = moves.ToList();
			}
		}

		public List<IEnumerable<Move>> GetAllLegalMovesForSide(GameModel game, PlayerColor side)
		{
			if (game == null) throw new ArgumentNullException(nameof(game));
			var legalMovesForSide = new List<IEnumerable<Move>>();
			foreach (var piece in BoardPieces.Where(p => p.Color == side))
			{
				var pseudoMoves = piece.GetPseudoLegalMoves(game);
				// Original code: legalMoves.Add(game.GameRules.FilterLegalMoves(game, moves));
				// This implies FilterLegalMoves takes (GameModel, IEnumerable<Move>)
				// but the interface IGameRules has FilterLegalMoves(GameModel, IChessPieceModel, IEnumerable<Move>)
				// Assuming a version of FilterLegalMoves that matches is available or the call is adjusted.
				// For now, let's assume the intent is to filter moves for *this* piece.
				var filteredMoves = game.GameRules.FilterLegalMoves(game, pseudoMoves);
				if (filteredMoves.Any())
				{
					legalMovesForSide.Add(filteredMoves);
				}
			}

			return legalMovesForSide;
		}

		public void SetEnPassantTargetSquare(IChessBoardSquareModel? enPassantSquare) =>
			EnPassantTargetSquare = enPassantSquare;

	#endregion

		internal void MovePieceInternal(IChessPieceModel pieceToMove, BoardPosition from, BoardPosition to)
		{
			if (pieceToMove == null) throw new ArgumentNullException(nameof(pieceToMove));
			if (!new BoardPosition(from.Column, from.Rank, Width, Height).IsValid())
				throw new InvalidOperationException($"Source position {from} is out of bounds for this board.");

			if (!new BoardPosition(to.Column, to.Rank, Width, Height).IsValid())
				throw new InvalidOperationException($"Target position {to} is out of bounds for this board.");

			var fromSquare = Squares[from.Column, from.Rank];
			var toSquare   = Squares[to.Column, to.Rank];

			if (fromSquare.GetPiece() != pieceToMove)
			{
				if (!(PieceIndex.TryGetValue(pieceToMove, out var actualPos) && actualPos.Equals(from)))
				{
					throw new InvalidOperationException(
						$"Piece {pieceToMove.GetPieceType()} ({pieceToMove.Color}) is not at the source position {from}. Recorded at: {(PieceIndex.TryGetValue(pieceToMove, out var p) ? p.Algebraic : "N/A")}, expected {from.Algebraic}");
				}
			}

			var pieceAtTarget = toSquare.GetPiece();
			if (pieceAtTarget != null)
			{
				if (pieceAtTarget == pieceToMove)
				{
					if (from.Equals(to))
					{
						PieceIndex[pieceToMove] = to;
						return;
					}
				}

				PieceIndex.Remove(pieceAtTarget);
				BoardPieces.Remove(pieceAtTarget);
			}

			fromSquare.SetPiece(null);
			toSquare.SetPiece(pieceToMove);
			PieceIndex[pieceToMove] = to;
			// Ensure piece is in BoardPieces if it wasn't (e.g. piece dropped on board)
			if (!BoardPieces.Contains(pieceToMove))
			{
				BoardPieces.Add(pieceToMove);
			}
		}

		internal void UpdateIndex(IChessPieceModel piece, BoardPosition newPos) =>
			// Ensure BoardPosition newPos is created with this board's dimensions for consistency
			PieceIndex[piece] = new(newPos.Column, newPos.Rank, Width, Height);

		// Helper, assuming it's an extension method elsewhere, or defined here:
		private IChessBoardSquareModel? GetSquareAt(BoardPosition position)
		{
			if (position.Column >= 0    &&
				position.Column < Width &&
				position.Rank   >= 0    &&
				position.Rank   < Height)
			{
				return Squares[position.Column, position.Rank];
			}

			return null;
		}

		private IChessBoardSquareModel? ResolveEnPassantTarget(string enPassantFen)
		{
			// If FEN indicates no en passant square (e.g., "-", empty, or whitespace), return null.
			if (string.IsNullOrWhiteSpace(enPassantFen) || enPassantFen == "-")
			{
				return null;
			}

			BoardPosition targetPosition;
			try
			{
				// Parse the algebraic notation from the FEN string.
				// It's important that AlgebraicNotationUtils.FromAlgebraic can parse notation
				// without assuming a fixed 8x8 board, or that it allows specifying dimensions.
				// For this example, we assume it returns raw 0-indexed coordinates.
				targetPosition = AlgebraicNotationUtils.FromAlgebraic(enPassantFen);
			}
			catch (Exception ex) // Catch exceptions from FromAlgebraic (e.g., invalid format)
			{
				// Log this error appropriately in a real application using a proper logging framework.
				Console.WriteLine(
					$"Warning: Could not parse FEN en passant square '{enPassantFen}'. Error: {ex.Message}. Treating as no en passant target.");

				return null; // Gracefully handle malformed FEN en passant part
			}

			// Validate that the parsed coordinates are within the current board's actual dimensions.
			if (targetPosition.Column >= 0    &&
				targetPosition.Column < Width &&
				targetPosition.Rank   >= 0    &&
				targetPosition.Rank   < Height)
			{
				// Coordinates are valid for this board, return the corresponding square.
				return Squares[targetPosition.Column, targetPosition.Rank];
			}

			// The FEN string specified an en passant square that is outside this board's dimensions.
			// Log this warning appropriately.
			Console.WriteLine(
				$"Warning: FEN en passant square '{enPassantFen}' (parsed as {targetPosition.Column},{targetPosition.Rank}) is outside board dimensions {Width}x{Height}. Treating as no en passant target.");

			return null;
		}

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
			var rank      = Height - 1; // FEN starts from 8th rank (0-indexed: Height - 1)
			var file      = 0;          // FEN starts from a-file (0-indexed: 0)

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
					var piece = ChessUtils.GetPieceFromChar(symbol); // Assuming this returns IChessPieceModel
					if (file < Width && rank >= 0)                   // Basic bounds check before accessing boardSquares
					{
						var square = boardSquares[file, rank];
						square.SetPiece(piece);
						pieceList.Add(piece);
						// Ensure BoardPosition stored in PieceIndex uses this board's dimensions
						UpdateIndex(piece, new(file, rank, Width, Height));
						file++;
					}
					else
					{
						// FEN string piece placement is out of bounds for declared board size.
						Console.WriteLine(
							$"Warning: FEN piece placement for '{symbol}' at calculated ({file},{rank}) is out of bounds for board size {Width}x{Height}.");

						// Depending on strictness, either throw, or skip this piece, or adjust file/rank.
						// For now, just skip if it would cause an error.
						if (file >= Width && symbol != '/')
							file = 0; // Attempt to recover if file overflowed before rank decrement
					}
				}
			}

			return pieceList;
		}
	}
}
