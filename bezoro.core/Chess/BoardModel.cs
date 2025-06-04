using System;
using System.Collections.Generic;
using System.Text;
using Bezoro.Core.Chess.Interfaces;
using Bezoro.Core.Chess.Utils;

namespace Bezoro.Core.Chess
{
	// Assuming PlayerColor enum exists, e.g.:
	// public enum PlayerColor { White, Black }

	/// <summary>
	///     Represents a chess board model that manages pieces, their positions and game state.
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

			var setup = boardSetup ?? FenUtility.StartBoard;

			Width          = width;
			Height         = height;
			Squares        = InitializeSquares(Width, Height);
			BoardPieces    = InitializePieces(Squares, setup.PiecePlacement);
			CapturedPieces = new(32);

			// Initialize game state essentials
			ActiveColor    = setup.ActiveColor;
			CastlingRights = setup.Castling;

			// Convert EnPassant string from FenData to BoardPosition?
			EnPassantTargetSquare = string.Equals(setup.EnPassant, "-", StringComparison.Ordinal)
				? null
				: AlgebraicNotationUtils.FromAlgebraic(
					setup.EnPassant);

			HalfmoveClock  = setup.HalfmoveClock;
			FullmoveNumber = setup.FullmoveNumber;
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

		public IChessBoardSquareModel[,] Squares { get; }
		public int                       Height  { get; }
		public int                       Width   { get; }

		public List<IChessPieceModel> BoardPieces { get; }
		public BoardPosition? EnPassantTargetSquare { get; internal set; }
		public CastlingRights CastlingRights { get; internal set; }
		public int FullmoveNumber { get; internal set; } // Starts at 1, increments after Black's move
		public int HalfmoveClock { get; internal set; } // For 50-move rule
		public List<IChessPieceModel> CapturedPieces { get; set; }
		public PlayerColor ActiveColor { get; internal set; } // Whose turn it is

	#region Interface Implementations

		public bool TryMovePiece(MovePieceCommand movePieceCommand)
		{
			if (movePieceCommand == null)
				throw new ArgumentNullException(nameof(movePieceCommand));

			movePieceCommand.Execute(this);
			InvalidateSnapshot();
			return true;
		}

		public void SetPieceAt(IChessPieceModel pieceToMove, IChessBoardSquareModel to)
		{
			if (pieceToMove == null) throw new ArgumentNullException(nameof(pieceToMove));
			if (to          == null) throw new ArgumentNullException(nameof(to));

			if (_pieceIndex.TryGetValue(pieceToMove, out var oldPos))
				GetSquare(oldPos).SetPiece(null);

			to.SetPiece(pieceToMove);
			UpdateIndex(pieceToMove, to.Position);

			InvalidateSnapshot();
		}

		public IChessBoardSquareModel GetSquare(BoardPosition position)
		{
			if (!IsValid(position))
				throw new ArgumentOutOfRangeException(nameof(position), "Position is out of bounds.");

			return Squares[position.Column, position.Row];
		}

		public bool IsEmpty(BoardPosition to) =>
			GetPieceAt(to) == null;

		public BoardPosition? GetPosition(IChessPieceModel piece) =>
			_pieceIndex.TryGetValue(piece, out var pos) ? pos : null;

		public IEnumerable<IChessBoardSquareModel> GetStraightPath(
			BoardPosition from,
			BoardPosition to)
		{
			if (from == null) throw new ArgumentNullException(nameof(from));
			if (to   == null) throw new ArgumentNullException(nameof(to));

			var dx = Math.Sign(to.File - from.File);
			var dy = Math.Sign(to.Rank - from.Rank);

			if (dx == 0 && dy == 0)
				throw new InvalidOperationException("Source and target squares are identical.");

			if (dx != 0 && dy != 0)
			{
				throw new InvalidOperationException("Path must be horizontal or vertical.");
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

		public void MovePiece(
			IChessPieceModel piece,
			BoardPosition from,
			BoardPosition to)
			=> MovePieceInternal(piece, from, to);

		public void MovePiece(
			IChessPieceModel piece,
			string fromAlgebraic,
			string toAlgebraic)
		{
			var from = AlgebraicNotationUtils.FromAlgebraic(fromAlgebraic);
			var to   = AlgebraicNotationUtils.FromAlgebraic(toAlgebraic);
			MovePieceInternal(piece, from, to);
		}

	#endregion

		public bool IsSquareAttacked(BoardPosition position, PlayerColor attackerColor)
		{
			if (position == null) throw new ArgumentNullException(nameof(position));
			return Snapshot.IsSquareAttacked(position, attackerColor);
		}

		public IChessPieceModel? GetPieceAt(BoardPosition position)
		{
			if (!IsValid(position))
				return null;

			return Squares[position.Column, position.Row].GetPiece();
		}

		public string ToFenString()
		{
			var fen = new StringBuilder();

			// 1. Piece placement
			for (var rank = Height - 1 ; rank >= 0 ; rank--)
			{
				var emptySquares = 0;
				for (var file = 0 ; file < Width ; file++)
				{
					var piece = GetPieceAt(new(file, rank));
					if (piece == null)
					{
						emptySquares++;
					}
					else
					{
						if (emptySquares > 0)
						{
							fen.Append(emptySquares);
							emptySquares = 0;
						}

						fen.Append(ChessUtils.GetCharFromPiece(piece));
					}
				}

				if (emptySquares > 0)
				{
					fen.Append(emptySquares);
				}

				if (rank > 0)
				{
					fen.Append('/');
				}
			}

			fen.Append(' ');

			// 2. Active color
			fen.Append(ActiveColor == PlayerColor.White ? 'w' : 'b');
			fen.Append(' ');

			// 3. Castling availability
			var castlingStr                                                        = "";
			if (CastlingRights.HasFlag(CastlingRights.WhiteKingSide)) castlingStr  += "K";
			if (CastlingRights.HasFlag(CastlingRights.WhiteQueenSide)) castlingStr += "Q";
			if (CastlingRights.HasFlag(CastlingRights.BlackKingSide)) castlingStr  += "k";
			if (CastlingRights.HasFlag(CastlingRights.BlackQueenSide)) castlingStr += "q";
			fen.Append(string.IsNullOrEmpty(castlingStr) ? "-" : castlingStr);
			fen.Append(' ');

			// 4. En passant target square
			fen.Append(EnPassantTargetSquare?.Algebraic ?? "-"); // Assumes BoardPosition has an Algebraic property
			fen.Append(' ');

			// 5. Halfmove clock
			fen.Append(HalfmoveClock);
			fen.Append(' ');

			// 6. Fullmove number
			fen.Append(FullmoveNumber);

			return fen.ToString();
		}

		internal void AddPieceToCaptured(IChessPieceModel piece)
		{
			if (piece == null) throw new ArgumentNullException(nameof(piece));
			CapturedPieces.Add(piece);
			InvalidateSnapshot();
		}

		internal void ClearCastlingRight(CastlingRights rightsToRemove)
		{
			CastlingRights &= ~rightsToRemove;
			InvalidateSnapshot();
		}

		internal void ClearPieceFromSquare(IChessBoardSquareModel square)
		{
			if (square == null)
				throw new ArgumentNullException(nameof(square));

			var piece = square.GetPiece();
			if (piece == null)
				return;

			_pieceIndex.Remove(piece);
			BoardPieces.Remove(piece);
			square.SetPiece(null);
			InvalidateSnapshot();
		}

		internal void UpdateIndex(IChessPieceModel piece, BoardPosition newPos)
			=> _pieceIndex[piece] = newPos;

		private BoardSnapshot CreateSnapshot() =>
			new(Squares);

		private bool IsValid(BoardPosition position) =>
			position.Column    >= 0
			&& position.Column < Width
			&& position.Row    >= 0
			&& position.Row    < Height;

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
			IChessBoardSquareModel[,] squares,
			string piecePlacement)
		{
			var pieceList = new List<IChessPieceModel>();
			var row       = Height - 1;
			var col       = 0;

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
					col++;
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
			if (currentFile >= Width || currentRank < 0 || currentFile < 0 || currentRank >= Height)
				return;

			var piece  = ChessUtils.GetPieceFromChar(pieceSymbol);
			var square = boardSquares[currentFile, currentRank];

			square.SetPiece(piece);
			piecesOnBoard.Add(piece);
			UpdateIndex(piece, new(currentFile, currentRank));
		}

		private void InvalidateSnapshot() =>
			_snapshotValid = false;

		private void MovePieceInternal(
			IChessPieceModel pieceToMove,
			BoardPosition from,
			BoardPosition to)
		{
			if (pieceToMove == null) throw new ArgumentNullException(nameof(pieceToMove));
			if (!IsValid(from)) throw new InvalidOperationException($"Position {from} is out of bounds.");
			if (!IsValid(to)) throw new InvalidOperationException($"Position {to} is out of bounds.");

			if (!_pieceIndex.TryGetValue(pieceToMove, out var current) || current != from)
			{
				throw new InvalidOperationException(
					$"Piece {pieceToMove} is recorded on {current.Algebraic}, not on {from.Algebraic}.");
			}

			var fromSquare = GetSquare(from);
			var toSquare   = GetSquare(to);

			if (fromSquare.GetPiece() != pieceToMove)
				throw new InvalidOperationException($"Piece {pieceToMove} is not at {from}.");

			var capturedPiece = toSquare.GetPiece();
			if (capturedPiece != null)
			{
				if (capturedPiece == pieceToMove)
				{
					throw new InvalidOperationException("Piece cannot capture itself by moving to its own square.");
				}

				_pieceIndex.Remove(capturedPiece);
				BoardPieces.Remove(capturedPiece);
			}

			fromSquare.SetPiece(null);
			toSquare.SetPiece(pieceToMove);
			_pieceIndex[pieceToMove] = to;

			InvalidateSnapshot();
		}
	}
}
