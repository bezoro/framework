using System;
using System.Collections.Generic;
using System.Linq;
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

		private static readonly (int dx, int dy)[] _diagonalDirs =
			{ (-1, 1), (1, 1), (1, -1), (-1, -1) }; // bishop
		// Direction sets (file offset, rank offset)
		private static readonly (int dx, int dy)[] _orthogonalDirs =
			{ (-1, 0), (1, 0), (0, -1), (0, 1) }; // rook

		private readonly Dictionary<IChessPieceModel, BoardPosition> _pieceIndex = new();
		/// <summary>
		///     Gets the two-dimensional array of board squares.
		/// </summary>
		public IChessBoardSquareModel[,] Squares { get; }
		/// <summary>
		///     Gets the height of the chess board.
		/// </summary>
		public int Height { get; }
		/// <summary>
		///     Gets the width of the chess board.
		/// </summary>
		public int Width { get; }

		/// <summary>
		///     Gets the list of pieces currently on the board.
		/// </summary>
		public List<IChessPieceModel> BoardPieces { get; }

		/// <summary>
		///     Gets the current castling rights for both players.
		/// </summary>
		public CastlingRights CastlingRights { get; internal set; } = CastlingRights.All;
		/// <summary>
		///     Gets or sets the list of captured pieces.
		/// </summary>
		public List<IChessPieceModel> CapturedPieces { get; set; }

	#region Interface Implementations

		/// <summary>
		///     Attempts to execute a move piece command on the board.
		/// </summary>
		/// <param name="movePieceCommand">The command to execute.</param>
		/// <returns>True if the move was successful; otherwise, false.</returns>
		/// <exception cref="ArgumentNullException">Thrown when movePieceCommand is null.</exception>
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

		/// <summary>
		///     Sets a piece at the specified board square **and updates the
		///     internal index so the position can be queried in O(1)**.
		/// </summary>
		/// <param name="pieceToMove">The piece to place on the square.</param>
		/// <param name="to">The destination square.</param>
		public void SetPieceAt(IChessPieceModel pieceToMove, IChessBoardSquareModel to)
		{
			if (pieceToMove == null) throw new ArgumentNullException(nameof(pieceToMove));
			if (to          == null) throw new ArgumentNullException(nameof(to));

			to.SetPiece(pieceToMove);
			UpdateIndex(pieceToMove, to.Position);
		}

		/// <summary>
		///     Gets the current position of a piece on the board.
		/// </summary>
		/// <param name="piece">The piece to locate.</param>
		/// <returns>The position of the piece if found; otherwise, null.</returns>
		public BoardPosition? GetPosition(IChessPieceModel piece) =>
			_pieceIndex.TryGetValue(piece, out var pos) ? pos : null;

	#endregion

		/// <summary>
		///     Returns all orthogonally adjacent squares to <paramref name="position" />.
		/// </summary>
		/// <param name="position">Origin square.</param>
		/// <param name="includeDiagonals">
		///     When true, diagonal neighbours are included (total of 8).
		/// </param>
		public IEnumerable<IChessBoardSquareModel> GetAdjacentSquares(
			BoardPosition position,
			bool includeDiagonals = false)
		{
			if (position == null)
				throw new ArgumentNullException(nameof(position));

			var directions = includeDiagonals
				? new (int dx, int dy)[]
				{
					(-1, 0), (1, 0), (0, -1), (0, 1),  // orthogonal
					(-1, -1), (1, -1), (1, 1), (-1, 1) // diagonal
				}
				: new (int dx, int dy)[]
				{
					(-1, 0), (1, 0), (0, -1), (0, 1) // orthogonal only
				};

			foreach (var (dx, dy) in directions)
			{
				var file = position.File + dx;
				var rank = position.Rank + dy;

				if (file >= 0 && file < Width && rank >= 0 && rank < Height)
				{
					yield return Squares[file, rank];
				}
			}
		}

		/// <summary>Squares a bishop could reach (ignoring check).</summary>
		public IEnumerable<IChessBoardSquareModel> GetDiagonalSquares(BoardPosition origin) =>
			GetSlidingSquares(origin, _diagonalDirs);

		/// <summary>Squares a rook could reach (ignoring check).</summary>
		public IEnumerable<IChessBoardSquareModel> GetOrthogonalSquares(BoardPosition origin) =>
			GetSlidingSquares(origin, _orthogonalDirs);

		/// <summary>Squares a queen could reach (ignoring check).</summary>
		public IEnumerable<IChessBoardSquareModel> GetQueenSquares(BoardPosition origin) =>
			GetSlidingSquares(origin, _orthogonalDirs.Concat(_diagonalDirs));

		/// <summary>
		///     Enumerates all squares reachable from <paramref name="origin" /> by sliding
		///     in any of the supplied <paramref name="directions" /> until the edge of
		///     the board or an occupied square is met.
		///     • The blocking square (if any) is included in the sequence.
		///     • Returned squares are in board order (closest first).
		/// </summary>
		public IEnumerable<IChessBoardSquareModel> GetSlidingSquares(
			BoardPosition origin,
			IEnumerable<(int dx, int dy)> directions)
		{
			if (origin     == null) throw new ArgumentNullException(nameof(origin));
			if (directions == null) throw new ArgumentNullException(nameof(directions));

			foreach (var (dx, dy) in directions)
			{
				foreach (var square in WalkRay(origin, dx, dy))
				{
					yield return square;
				}
			}
		}

		/// <summary>
		///     Enumerates every square in a straight path between <paramref name="from" />
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

		/// <summary>
		///     Walks in a single direction (dx, dy) starting next to <paramref name="from" />.
		///     Stops when it leaves the board or hits an occupied square (which is yielded).
		/// </summary>
		private IEnumerable<IChessBoardSquareModel> WalkRay(BoardPosition from, int dx, int dy)
		{
			var f = from.File + dx;
			var r = from.Rank + dy;

			while (f >= 0 && f < Width && r >= 0 && r < Height)
			{
				var sq = Squares[f, r];
				yield return sq;

				// If the square is occupied, the piece blocks further travel on this ray.
				if (sq.GetPiece() != null) yield break;

				f += dx;
				r += dy;
			}
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
	}
}
