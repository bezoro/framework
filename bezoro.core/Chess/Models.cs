using System;
using System.Collections.Generic;
using Bezoro.Core.Chess.Utils;

namespace Bezoro.Core.Chess
{
	public class ChessBoardModel : IChessBoardModel
	{
		public ChessBoardModel(int width = 8, int height = 8, FenData? boardSetup = null)
		{
			if (width  <= 0) throw new ArgumentOutOfRangeException(nameof(width),  "Width must be positive.");
			if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive.");

			boardSetup ??= FenUtility.StandardBoard;
			Width          = width;
			Height         = height;
			Squares        = InitializeSquares(Width, Height);
			BoardPieces    = InitializePieces(Squares, boardSetup);
			CapturedPieces = new IChessPieceModel[32];
		}

		public IChessBoardSquareModel[,] Squares        { get; }
		public IChessPieceModel[]        BoardPieces    { get; }
		public int                       Height         { get; }
		public int                       Width          { get; }
		public IChessPieceModel[]        CapturedPieces { get; set; }

		public bool TryMovePiece(IChessPieceModel pieceToMove, MoveCommand command)
		{
			var targetFile = command.To.File;
			var targetRank = command.To.Rank;

			// 1. Validate target coordinates
			if (targetFile < 0 || targetFile >= Width || targetRank < 0 || targetRank >= Height)
			{
				return false; // Target square is off-board
			}

			// 2. Get the target square
			var targetSquare = Squares[targetFile, targetRank];

			// 3. Get the piece currently at the target square (if any)
			var pieceAtTarget = targetSquare.Piece;

			// 4. Clear the piece from its original square
			// It's assumed 'piece' is currently on the board and piece.Square is not null.
			if (pieceToMove.Square != null)
			{
				pieceToMove.Square.Piece = null;
			}

			// 5. Update the moving piece's properties
			pieceToMove.Position = targetSquare.Position; // Update position to the target square's position
			pieceToMove.Square   = targetSquare;          // Update the piece's reference to its new square

			// 6. Place the moving piece on the target square
			targetSquare.Piece = pieceToMove;

			// 7. Handle capture
			if (pieceAtTarget    != null
				&& pieceAtTarget != pieceToMove) // Check if there was a piece and it's not the moving piece itself
			{
				pieceAtTarget.IsCaptured = true;
				// Note: The captured piece still exists in the main `Pieces` array.
				// Depending on game requirements, you might want to remove it from active pieces list
				// or move it to a separate list of captured pieces.
				// For now, marking IsCaptured = true is consistent with the IChessPieceModel.
			}

			return true; // Move was successful
		}

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

		private IChessPieceModel[] InitializePieces(IChessBoardSquareModel[,] squares, FenData boardSetup)
		{
			var pieceList    = new List<IChessPieceModel>();
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
				else // Piece character
				{
					CreatePieceAtFromSymbol(symbol, col, row, squares, pieceList);
					col++; // Move to the next file for the next piece or number
				}
			}

			return pieceList.ToArray();
		}

		/// <summary>
		///     Handles a FEN piece symbol by creating a chess piece,
		///     placing it on the board if the coordinates are valid,
		///     and adding it to the list of pieces.
		/// </summary>
		/// <param name="pieceSymbol">The FEN character representing the piece.</param>
		/// <param name="currentFile">The current file (column index) for piece placement.</param>
		/// <param name="currentRank">The current rank (row index) for piece placement.</param>
		/// <param name="boardSquares">The 2D array of board squares.</param>
		/// <param name="piecesOnBoard">The list to add the created piece to.</param>
		private void CreatePieceAtFromSymbol(
			char pieceSymbol,
			int currentFile,
			int currentRank,
			IChessBoardSquareModel[,] boardSquares,
			List<IChessPieceModel?> piecesOnBoard)
		{
			var pieceColor = char.IsUpper(pieceSymbol) ? PlayerColor.White : PlayerColor.Black;
			var pieceType  = ChessUtils.GetPieceTypeFromChar(char.ToLower(pieceSymbol));
			var chessPiece = new ChessPieceModel(pieceColor, pieceType);

			// Ensure indices are within bounds
			if (currentFile >= Width || currentRank < 0)
				return;

			var square = boardSquares[currentFile, currentRank];

			chessPiece.Position = square.Position;
			chessPiece.Square   = square;
			square.Piece        = chessPiece;

			piecesOnBoard.Add(chessPiece);
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
