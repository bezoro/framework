using Bezoro.Chess.Abstractions.Interfaces;
using Bezoro.Chess.Board;
using Bezoro.Chess.Board.Models;
using Bezoro.Chess.Common.Enums;
using Bezoro.Chess.Common.Extensions;
using Bezoro.Chess.Common.Helpers;
using Bezoro.Chess.Game.Models;
using Bezoro.Chess.Moves.Models;

namespace Bezoro.Chess.UnitTests.Common.Extensions;

[TestFixture]
[TestOf(typeof(BoardModelExtensions))]
public class BoardModelExtensionsUnitTests
{
	private IChessBoardModel _board;

#region Setup/Teardown Methods

	[SetUp]
	public void SetUp() =>
		_board = new BoardModel(8, 8, FenUtils.EmptyBoard);

#endregion

#region Happy paths ----------------------------------------------------

	[Test]
	public void GetSquareAt_String_ReturnsExpectedSquare()
	{
		var expected = _board.Squares[4, 3]; // e4 -> (4,3)
		var actual   = _board.GetSquareAt("e4");

		Assert.That(actual, Is.SameAs(expected));
	}

	[Test]
	public void GetPieceAt_String_ReturnsExpectedPiece()
	{
		var rook = new FakePiece();
		_board.Squares[4, 3].SetPiece(rook); // place on e4

		var actual = _board.GetPieceAt("e4");

		Assert.That(actual, Is.SameAs(rook));
	}

	[Test]
	public void GetPiecePlacementFen_EmptyBoard_ReturnsAllEmptyString()
	{
		var fen = _board.GetPiecePlacementFen();

		Assert.That(fen, Is.EqualTo("8/8/8/8/8/8/8/8"));
	}

	[Test]
	public void CreatePieceAt_String_PlacesPieceOnTargetSquare()
	{
		var emptyBoard = new BoardModel(8, 8, FenUtils.EmptyBoard);
		Assert.That(emptyBoard.GetPieceAt("a1"), Is.Null);

		_board.CreatePieceAt("a1", PlayerColor.White, ChessPieceType.King);

		Assert.That(_board.GetPieceAt("a1"),                Is.Not.Null);
		Assert.That(_board.GetPieceAt("a1").Color,          Is.EqualTo(PlayerColor.White));
		Assert.That(_board.GetPieceAt("a1").GetPieceType(), Is.EqualTo(ChessPieceType.King));
		Assert.That(_board.GetPieceAt("a1").HasMoved,       Is.False);
	}

#endregion

#region Minimal fakes --------------------------------------------------

	private sealed class FakeBoard : IChessBoardModel
	{
		public FakeBoard(int width, int height)
		{
			Width   = width;
			Height  = height;
			Squares = new IChessBoardSquareModel[width, height];

			for (var x = 0 ; x < width ; x++)
			{
				for (var y = 0 ; y < height ; y++)
					Squares[x, y] = new FakeSquare(new(x, y));
			}
		}

		public Dictionary<IChessPieceModel, BoardPosition> PieceIndex            { get; }
		public IChessBoardSquareModel                      EnPassantTargetSquare { get; }

		public IChessBoardSquareModel[,] Squares { get; }
		public int                       Height  { get; }

		public int                                               Width                  { get; }
		public IReadOnlyDictionary<IChessPieceModel, List<Move>> CachedPseudoLegalMoves { get; }

		/* --- members not required by the extension code --- */
		public List<IChessPieceModel> BoardPieces => new();

	#region Interface Implementations

		public BoardPosition? GetPosition(IChessPieceModel piece) => null;

		public IEnumerable<IChessBoardSquareModel> GetStraightPath(BoardPosition from, BoardPosition to)
			=> throw new NotImplementedException();

		public bool IsEmpty(BoardPosition position) => this.GetSquareAt(position.Column, position.Row).IsEmpty;
		public bool IsSquareAttacked(BoardPosition position, PlayerColor byColor) => false;

		public IReadOnlyList<Move> GetCachedMovesFor(IChessPieceModel piece) =>
			throw new NotImplementedException();

		public void MovePieceTo(IChessPieceModel piece, BoardPosition from, BoardPosition to) =>
			throw new NotImplementedException();

		public void SetPieceAt(IChessPieceModel piece, IChessBoardSquareModel square) =>
			square.SetPiece(piece);

		public void PerformCastle(IChessPieceModel rook, CastleSide side) =>
			throw new NotImplementedException();

		public void CapturePieceAt(IChessPieceModel pieceToCapture, BoardPosition pos, GameModel game) =>
			throw new NotImplementedException();

		public void RestoreLastCapturedPiece(
			ChessPieceType capturedPieceType,
			BoardPosition capturedPosition,
			GameModel game) =>
			throw new NotImplementedException();

		public List<IEnumerable<Move>> GetAllLegalMovesForSide(GameModel game, PlayerColor side) =>
			throw new NotImplementedException();

		public void SetEnPassantTargetSquare(IChessBoardSquareModel? enPassantSquare) =>
			throw new NotImplementedException();

		public IChessBoardModel Clear() =>
			throw new NotImplementedException();

		public bool IsEnemy(IChessBoardSquareModel targetSquare, PlayerColor myColor) =>
			throw new NotImplementedException();

	#endregion

		public bool IsInside(int col, int row) =>
			col >= 0 && col < Width && row >= 0 && row < Height;
	}

	private sealed class FakeSquare : IChessBoardSquareModel
	{
		public FakeSquare(BoardPosition position)
		{
			Position = position;
		}

		public BoardPosition Position { get; }

		public bool IsEmpty    => Piece is null;
		public bool IsOccupied => Piece is not null;

		public IChessPieceModel? Piece { get; private set; }

	#region Interface Implementations

		public void SetPiece(IChessPieceModel? piece) => Piece = piece;
		public void ClearPiece() => Piece = null;
		public IChessPieceModel? GetPiece() => Piece;
		public void RemovePiece(IChessPieceModel piece) => ClearPiece();

	#endregion
	}

	private sealed class FakePiece : IChessPieceModel
	{
		public PlayerColor Color    => PlayerColor.White;
		public PlayerColor Opposite => PlayerColor.Black;
		public bool        HasMoved { get; private set; }

	#region Interface Implementations

		public ChessPieceType GetPieceType() => ChessPieceType.Rook;
		public IEnumerable<Move> GetPseudoLegalMoves(GameModel _) => Array.Empty<Move>();
		public void MarkMoved() => HasMoved = true;
		public void ResetMoved() => HasMoved = false;

		public void ToggleMoved() =>
			throw new NotImplementedException();

		public void SetMoved(bool value) =>
			throw new NotImplementedException();

	#endregion
	}

#endregion
}
