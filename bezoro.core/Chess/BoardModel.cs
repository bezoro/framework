using System;
using System.Collections.Generic;
using Bezoro.Core.Chess.Utils;

namespace Bezoro.Core.Chess
{
	/// <summary>
	///     Represents a chess board model that manages pieces, their positions, and game state.
	/// </summary>
	public class BoardModel : IChessBoardModel
	{
		/// <summary>
		///     Initializes a new instance of the chess board model.
		/// </summary>
		/// <param name="width">The width of the board. Defaults to 8.</param>
		/// <param name="height">The height of the board. Defaults to 8.</param>
		/// <param name="boardSetup">Optional FEN data for initial board setup. If null, uses standard chess setup.</param>
		/// <exception cref="ArgumentOutOfRangeException">Thrown when width or height is not positive.</exception>
		public BoardModel(int width = 8, int height = 8, FenData? boardSetup = null)
		{
			if (width  <= 0) throw new ArgumentOutOfRangeException(nameof(width),  "Width must be positive.");
			if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height), "Height must be positive.");

			boardSetup     ??= FenUtility.StartBoard;
			Width          =   width;
			Height         =   height;
			Squares        =   InitializeSquares(Width, Height);
			BoardPieces    =   InitializePieces(Squares, boardSetup.Value.PiecePlacement);
			CapturedPieces =   new(32);
		}

		private BoardSnapshot? _cachedSnapshot;
		private bool           _snapshotValid; // false after every mutation

		private readonly Dictionary<IChessPieceModel, BoardPosition> _pieceIndex = new();

		/// <summary>
		///     Returns a read-only picture of the current position.
		///     The object is created once per position and reused until the
		///     board changes again.
		/// </summary>
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

		public IChessBoardSquareModel[,] Squares { get; }
		public int                       Height  { get; }
		public int                       Width   { get; }

		public List<IChessPieceModel> BoardPieces    { get; }
		public CastlingRights         CastlingRights { get; internal set; } = CastlingRights.All;
		public List<IChessPieceModel> CapturedPieces { get; set; }

	#region Interface Implementations

		public bool IsSquareAttacked(IChessBoardSquareModel sq, PlayerColor opposite) =>
			throw new NotImplementedException();

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
			if (pieceToMove == null) throw new ArgumentNullException(nameof(pieceToMove));
			if (to          == null) throw new ArgumentNullException(nameof(to));

			to.SetPiece(pieceToMove);
			UpdateIndex(pieceToMove, to.Position);
		}

		public bool IsEmpty(BoardPosition to)
		{
			if (to == null)
				throw new ArgumentNullException(nameof(to));

			return Squares[to.File, to.Rank].GetPiece() == null;
		}

		public BoardPosition? GetPosition(IChessPieceModel piece) =>
			_pieceIndex.TryGetValue(piece, out var pos) ? pos : null;

	#endregion

		/// <summary>
		///     Lists every square in a straight path between <paramref name="from" />
		///     and <paramref name="to" />, excluding the endpoints.
		///     Ideal for validating that all squares between the king and rook are empty
		///     when evaluating castling rights.
		/// </summary>
		/// <exception cref="InvalidOperationException">
		///     Thrown when the two squares are not on the same rank or file.
		/// </exception>
		public IEnumerable<IChessBoardSquareModel> GetStraightPath(
			BoardPosition from,
			BoardPosition to)
		{
			if (from == null) throw new ArgumentNullException(nameof(from));
			if (to   == null) throw new ArgumentNullException(nameof(to));

			var dx = Math.Sign(to.File - from.File);
			var dy = Math.Sign(to.Rank - from.Rank);

			// Must be purely horizontal or vertical for castling.
			if (dx != 0 && dy != 0)
			{
				throw new InvalidOperationException(
					"Path must be horizontal or vertical.");
			}

			var curFile = from.File + dx;
			var curRank = from.Rank + dy;

			while (curFile != to.File || curRank != to.Rank)
			{
				yield return Squares[curFile, curRank];
				curFile += dx;
				curRank += dy;
			}
		}

		internal void ClearCastlingRight(CastlingRights rightsToRemove)
			=> CastlingRights &= ~rightsToRemove;

		/// <summary>
		///     Removes a piece from the specified square and from the index.
		///     (Useful for captures and undo logic.)
		/// </summary>
		/// <param name="square">The square from which to remove a piece.</param>
		internal void ClearSquare(IChessBoardSquareModel square)
		{
			if (square == null)
				throw new ArgumentNullException(nameof(square));

			var piece = square.GetPiece();
			if (piece != null)
				_pieceIndex.Remove(piece);

			square.SetPiece(null);
		}

		internal void UpdateIndex(IChessPieceModel piece, BoardPosition newPos)
			=> _pieceIndex[piece] = newPos;

		private BoardSnapshot CreateSnapshot() =>
			throw new NotImplementedException();

		private static IChessBoardSquareModel[,] InitializeSquares(int width, int height)
		{
			var squares = new IChessBoardSquareModel[width, height];
			for (var row = 0 ; row < width ; row++)
			{
				for (var col = 0 ; col < height ; col++)
				{
					squares[row, col] = new BoardSquareModel(new(row, col));
				}
			}

			return squares;
		}

		private List<IChessPieceModel> InitializePieces(
			IChessBoardSquareModel[,] squares,
			string piecePlacement)
		{
			var pieceList = new List<IChessPieceModel>();

			var row = Height - 1; // FEN starts from 8th rank, array index is Height-1
			var col = 0;          // FEN starts from 'a' file, array index is 0

			foreach (var symbol in piecePlacement)
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
					CreatePieceAtFromSymbol(symbol, col, row, squares, pieceList);
					col++; // next file
				}
			}

			return pieceList;
		}

		private void CreatePieceAtFromSymbol(
			char pieceSymbol,
			int currentFile,
			int currentRank,
			IChessBoardSquareModel[,] boardSquares,
			List<IChessPieceModel> piecesOnBoard)
		{
			if (currentFile >= Width || currentRank < 0)
				return;

			var piece  = ChessUtils.GetPieceFromChar(pieceSymbol);
			var square = boardSquares[currentFile, currentRank];

			square.SetPiece(piece);
			piecesOnBoard.Add(piece);
			UpdateIndex(piece, new(currentFile, currentRank));
		}

		/// <summary>
		///     Must be called by every method that mutates the board state.
		/// </summary>
		private void InvalidateSnapshot() => _snapshotValid = false;
	}
}
