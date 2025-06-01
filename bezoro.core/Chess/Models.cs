using System;
using System.Collections.Generic;
using Bezoro.Core.Chess.Utils;

namespace Bezoro.Core.Chess
{
	public class ChessBoardModel : IChessBoardModel
	{
		public ChessBoardModel(int width, int height, FenData boardSetup)
		{
			if (width  <= 0) throw new ArgumentOutOfRangeException(nameof(width),  "Width must be positive.");
			if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive.");

			Width   = width;
			Height  = height;
			Pieces  = new IChessPieceModel[32];
			Squares = InitializeSquares(Width, Height);
			Pieces  = InitializePieces(Squares, boardSetup);
		}

		public IChessBoardSquareModel[,] Squares { get; }
		public IChessPieceModel?[]       Pieces  { get; }

		public int Height { get; }
		public int Width  { get; }

		private static IChessBoardSquareModel[,] InitializeSquares(int width, int height)
		{
			var squares = new IChessBoardSquareModel[width, height];
			for (var row = 0 ; row < width ; row++)
			{
				for (var col = 0 ; col < height ; col++)
				{
					squares[row, col] = new ChessBoardSquareModel(new(row, col));
				}
			}

			return squares;
		}

		private IChessPieceModel?[] InitializePieces(IChessBoardSquareModel[,] squares, FenData boardSetup)
		{
			var pieceList    = new List<IChessPieceModel?>();
			var fenPlacement = boardSetup.PiecePlacement;

			var row = Height - 1; // FEN starts from 8th rank, array index is Height-1
			var col = 0;          // FEN starts from 'a' file, array index is 0

			foreach (var symbol in fenPlacement)
			{
				if (symbol == '/')
				{
					row--;
					col = 0;
				}
				else if (char.IsDigit(symbol))
				{
					col += int.Parse(symbol.ToString());
				}
				else
				{
					var color = char.IsUpper(symbol) ? PlayerColor.White : PlayerColor.Black;
					var type  = ChessUtils.GetPieceTypeFromChar(char.ToLower(symbol));

					var piece = new ChessPieceModel(color, type);
					// Ensure indices are within bounds, especially if FEN doesn't match board dimensions
					if (col < Width && row >= 0)
					{
						var square = squares[col, row];

						piece.Position = square.Position;
						piece.Square   = square;
						square.Piece   = piece;

						pieceList.Add(piece);
					}
					// else: FEN char implies a piece outside board dimensions, decide handling (e.g., error or ignore)
					// For now, we'll just increment file index.

					col++;
				}
			}

			return pieceList.ToArray();
		}
	}

	public record ChessBoardSquareModel(ChessPosition Position) : IChessBoardSquareModel
	{
		public bool IsEmpty    => Piece == null;
		public bool IsOccupied => Piece != null;

		public bool              IsHighlightedAsValidMove { get; set; }
		public bool              IsSelected               { get; set; }
		public IChessPieceModel? Piece                    { get; set; }
	}

	public record ChessPieceModel(PlayerColor Color, ChessPieceType Type) : IChessPieceModel
	{
		public bool                   IsCaptured { get; set; }
		public bool                   IsSelected { get; set; }
		public ChessPosition          Position   { get; set; }
		public IChessBoardSquareModel Square     { get; set; }
	}
}
