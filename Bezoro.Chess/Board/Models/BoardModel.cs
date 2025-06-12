using System;
using System.Collections.Generic;
using Bezoro.Chess.Abstractions.Interfaces;
using Bezoro.Chess.Common.Enums;
using Bezoro.Chess.Common.Extensions;
using Bezoro.Chess.Common.Helpers;

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
		public BoardModel(uint width = 8, uint height = 8, string? piecePlacementFen = null, string enPassant = "-")
		{
			if (width  <= 0) throw new ArgumentOutOfRangeException(nameof(width),  "Width must be positive.");
			if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive.");

			piecePlacementFen ??= FenUtils.StartPiecePlacement.ToString();

			Width   = width;
			Height  = height;
			Squares = InitializeSquares(Width, Height);
			InitializePieces(Squares, piecePlacementFen);
			EnPassantSquare = ResolveEnPassantTarget(enPassant);
		}

		public IChessBoardSquareModel[,] Squares         { get; }
		public uint                      Height          { get; }
		public uint                      Width           { get; }
		public IChessBoardSquareModel?   EnPassantSquare { get; private set; }

	#region Interface Implementations

		/// <summary>
		///     Clears the board of pieces
		/// </summary>
		public IChessBoardModel ClearPieces() =>
			throw new NotImplementedException();

		/// <summary>
		///     Resets the entire board state to the starting setup.
		/// </summary>
		public IChessBoardModel Reset() =>
			throw new NotImplementedException();

		/// <summary>
		///     Moves the piece from the given position to the given position.
		/// </summary>
		/// <param name="from"></param>
		/// <param name="to"></param>
		public void MovePiece(BoardPosition from, BoardPosition to) =>
			throw new NotImplementedException();

		/// <summary>
		///     Sets the en passant square.
		/// </summary>
		/// <param name="enPassantSquare"></param>
		public void SetEnPassantSquare(IChessBoardSquareModel? enPassantSquare) =>
			EnPassantSquare = enPassantSquare;

		public void SetPieceAt(BoardPosition position, IChessPieceModel? piece) =>
			this.GetSquareAt(position).SetPiece(piece);

	#endregion

		public IChessPieceModel CreatePieceAt(PlayerColor color, ChessPieceType pieceType, BoardPosition position)
		{
			var square = this.GetSquareAt(position);

			if (square is null)
				throw new ArgumentException("No square at the given position.", nameof(position));

			var piece = ChessUtils.CreatePiece(color, pieceType);
			square.SetPiece(piece);
			return piece;
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

		private static IChessBoardSquareModel[,] InitializeSquares(uint width, uint height)
		{
			var squares = new IChessBoardSquareModel[width, height];
			for (uint file = 0 ; file < width ; file++)
			{
				for (uint rank = 0 ; rank < height ; rank++)
				{
					squares[file, rank] = new BoardSquareModel(file, rank);
				}
			}

			return squares;
		}

		private void InitializePieces(IChessBoardSquareModel[,] boardSquares, string piecePlacementFen)
		{
			var pieceList = new List<IChessPieceModel?>();
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
		}
	}
}
