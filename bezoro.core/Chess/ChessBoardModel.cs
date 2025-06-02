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

			boardSetup     ??= FenUtility.StandardBoard;
			Width          =   width;
			Height         =   height;
			Squares        =   InitializeSquares(Width, Height);
			BoardPieces    =   InitializePieces(Squares, boardSetup.PiecePlacement);
			CapturedPieces =   new(32);
		}

		public CastlingRights            CastlingRights { get; } = CastlingRights.All;
		public IChessBoardSquareModel[,] Squares        { get; }
		public int                       Height         { get; }
		public int                       Width          { get; }

		public List<IChessPieceModel> BoardPieces    { get; }
		public List<IChessPieceModel> CapturedPieces { get; set; }

	#region Interface Implementations

		public bool TryMovePiece(MovePieceCommand movePieceCommand)
		{
			if (movePieceCommand == null)
				throw new ArgumentNullException(nameof(movePieceCommand));

			try
			{
				movePieceCommand.Execute(this);
				return true;
			}
			catch (InvalidOperationException)
			{
				return false;
			}
		}

		public void SetPieceAt(IChessPieceModel pieceToMove, IChessBoardSquareModel to)
		{
			pieceToMove.Square.Piece = null;
			to.Piece                 = pieceToMove;
			pieceToMove.Square       = to;
			pieceToMove.Position     = to.Position;
		}

	#endregion

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

		private List<IChessPieceModel> InitializePieces(IChessBoardSquareModel[,] squares, string piecePlacement)
		{
			var pieceList = new List<IChessPieceModel>();

			var row = Height - 1; // FEN starts from 8th rank, array index is Height-1
			var col = 0;          // FEN starts from 'a' file, array index is 0

			foreach (var symbol in piecePlacement)
			{
				if (symbol == '/') // Move to the next rank (row)
				{
					row--;
					col = 0;
				}
				else if (char.IsDigit(symbol)) // Skip empty squares
				{
					col += int.Parse(symbol.ToString());
				}
				else // Found piece symbol - create and place the piece
				{
					CreatePieceAtFromSymbol(symbol, col, row, squares, pieceList);
					col++; // Move to the next file (column)
				}
			}

			return pieceList;
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
			List<IChessPieceModel> piecesOnBoard)
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
}
