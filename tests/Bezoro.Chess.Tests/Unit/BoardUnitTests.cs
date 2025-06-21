using Bezoro.Chess.Domain.Helpers;
using Bezoro.Chess.Domain.Shared.Enums;
using Bezoro.Chess.Domain.Types.Records;
using Bezoro.Chess.Domain.Types.Structs;

namespace Bezoro.Chess.Tests.Unit;

public sealed class BoardUnitTests
{
	private static readonly Position A1 = new(0, 0);

	private static readonly Position Center   = new(3, 3); // d4 – convenient test square
	private static readonly Position D4Square = new(3, 3); // central square d4
	private static readonly Position H8       = new(7, 7);

	[Fact]
	public void GetPiece_WhenSquareIsEmpty_ReturnsDefaultPiece()
	{
		// Arrange
		Board board = EmptyBoard();

		// Act
		Piece result = board.GetPiece(D4Square);

		// Assert
		Assert.Equal(default, result);
	}

	/* ---------- edge-case: corners -------------------------------------*/

	[Theory]
	[InlineData(0, 0)] // A1
	[InlineData(7, 7)] // H8
	public void SetPiece_CornerSquaresBehaveLikeAnyOther(int row, int col)
	{
		// Arrange
		var    pos   = new Position(row, col);
		Piece  king  = P(PieceType.King, PieceColor.White);
		Board? board = EmptyBoard();

		// Act
		Board updated = board.SetPiece(pos, king);

		// Assert
		Assert.Equal(king, updated.GetPiece(pos));
	}

	/* ---------- immutability -------------------------------------------*/

	[Fact]
	public void SetPiece_WhenAddingPiece_DoesNotModifyOriginalBoard()
	{
		// Arrange
		Board? original = EmptyBoard();
		Piece  pawn     = P(PieceType.Pawn, PieceColor.White);

		// Act
		Board mutated = original.SetPiece(Center, pawn);

		// Assert
		Assert.Equal(default, original.GetPiece(Center)); // still empty
		Assert.Equal(pawn,    mutated.GetPiece(Center));  // only new board has pawn
	}

	/* ---------- happy-path: adding -------------------------------------*/

	[Fact]
	public void SetPiece_WhenAddingPieceToEmptySquare_ReturnsBoardWithThatPiece()
	{
		// Arrange
		Board? original = EmptyBoard();
		Piece  queen    = P(PieceType.Queen, PieceColor.White);

		// Act
		Board result = original.SetPiece(Center, queen);

		// Assert
		Assert.Equal(queen,   result.GetPiece(Center));   // piece placed
		Assert.Equal(default, original.GetPiece(Center)); // original unchanged
	}

	/* ---------- removal -------------------------------------------------*/

	[Fact]
	public void SetPiece_WhenPassedDefaultPiece_ClearsTheSquare()
	{
		// Arrange
		Board boardWithPiece = EmptyBoard()
			.SetPiece(Center, P(PieceType.Bishop, PieceColor.Black));

		// Act
		Board result = boardWithPiece.SetPiece(Center, default);

		// Assert
		Assert.Equal(default, result.GetPiece(Center));
	}

	/* ---------- no-op scenarios (return SAME instance) -----------------*/

	[Fact]
	public void SetPiece_WhenPlacingIdenticalPiece_ReturnsSameInstance()
	{
		// Arrange
		Piece knight = P(PieceType.Knight, PieceColor.Black);
		Board board  = EmptyBoard().SetPiece(Center, knight);

		// Act
		Board result = board.SetPiece(Center, knight); // identical

		// Assert
		Assert.Same(board, result);
	}

	[Fact]
	public void SetPiece_WhenRemovingFromEmptySquare_ReturnsSameInstance()
	{
		// Arrange
		Board? board = EmptyBoard();

		// Act
		Board result = board.SetPiece(Center, default); // removing nothing

		// Assert
		Assert.Same(board, result);
	}

	/* ---------- happy-path: replacing ----------------------------------*/

	[Fact]
	public void SetPiece_WhenSquareAlreadyContainsDifferentPiece_ReplacesPiece()
	{
		// Arrange
		Board boardWithPawn = EmptyBoard()
			.SetPiece(Center, P(PieceType.Pawn, PieceColor.White));

		Piece rook = P(PieceType.Rook, PieceColor.White);

		// Act
		Board result = boardWithPawn.SetPiece(Center, rook);

		// Assert
		Assert.Equal(rook, result.GetPiece(Center));
	}

	[Theory]
	/* white */
	[InlineData(PieceType.Pawn,   PieceColor.White, "a2")]
	[InlineData(PieceType.Pawn,   PieceColor.White, "b2")]
	[InlineData(PieceType.Pawn,   PieceColor.White, "c2")]
	[InlineData(PieceType.Pawn,   PieceColor.White, "d2")]
	[InlineData(PieceType.Pawn,   PieceColor.White, "e2")]
	[InlineData(PieceType.Pawn,   PieceColor.White, "f2")]
	[InlineData(PieceType.Pawn,   PieceColor.White, "g2")]
	[InlineData(PieceType.Pawn,   PieceColor.White, "h2")]
	[InlineData(PieceType.Knight, PieceColor.White, "b1")]
	[InlineData(PieceType.Bishop, PieceColor.White, "c1")]
	[InlineData(PieceType.Rook,   PieceColor.White, "a1")]
	[InlineData(PieceType.Queen,  PieceColor.White, "d1")]
	[InlineData(PieceType.King,   PieceColor.White, "e1")]
	/* black */
	[InlineData(PieceType.Pawn,   PieceColor.Black, "a7")]
	[InlineData(PieceType.Pawn,   PieceColor.Black, "b7")]
	[InlineData(PieceType.Pawn,   PieceColor.Black, "c7")]
	[InlineData(PieceType.Pawn,   PieceColor.Black, "d7")]
	[InlineData(PieceType.Pawn,   PieceColor.Black, "e7")]
	[InlineData(PieceType.Pawn,   PieceColor.Black, "f7")]
	[InlineData(PieceType.Pawn,   PieceColor.Black, "g7")]
	[InlineData(PieceType.Pawn,   PieceColor.Black, "h7")]
	[InlineData(PieceType.Knight, PieceColor.Black, "b8")]
	[InlineData(PieceType.Bishop, PieceColor.Black, "c8")]
	[InlineData(PieceType.Rook,   PieceColor.Black, "a8")]
	[InlineData(PieceType.Queen,  PieceColor.Black, "d8")]
	[InlineData(PieceType.King,   PieceColor.Black, "e8")]
	internal void GetPiece_WhenSquareContainsPiece_ReturnsThatPiece(
		PieceType type, PieceColor color, string expectedPosition)
	{
		// Arrange
		Piece expected = P(type, color);
		var   board    = new Board(BoardFactory.CreateInitialBitboards());

		// Act
		Piece result = board.GetPiece(new Position(expectedPosition));

		// Assert
		Assert.Equal(expected, result);
	}

	private static Board EmptyBoard() => new(new BoardBitboards());
	private static Piece P(PieceType t, PieceColor c) => new(t, c);
}
