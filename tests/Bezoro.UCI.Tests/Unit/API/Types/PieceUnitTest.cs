using Bezoro.UCI.API.Common.Enums;
using Bezoro.UCI.API.Types;
using JetBrains.Annotations;

namespace Bezoro.UCI.Tests.Unit.API.Types;

[TestSubject(typeof(Piece))]
public class PieceUnitTest
{
	[Fact]
	public void FromChar_WhenValidParameters_ReturnsExpectedPiece()
	{
		var blackPawn   = Piece.FromChar('p');
		var blackRook   = Piece.FromChar('r');
		var blackKnight = Piece.FromChar('n');
		var blackBishop = Piece.FromChar('b');
		var blackQueen  = Piece.FromChar('q');
		var blackKing   = Piece.FromChar('k');

		var whitePawn   = Piece.FromChar('P');
		var whiteRook   = Piece.FromChar('R');
		var whiteKnight = Piece.FromChar('N');
		var whiteBishop = Piece.FromChar('B');
		var whiteQueen  = Piece.FromChar('Q');
		var whiteKing   = Piece.FromChar('K');

		Assert.All([blackPawn.Type, whitePawn.Type],     type => Assert.Equal(PieceType.Pawn,   type));
		Assert.All([blackRook.Type, whiteRook.Type],     type => Assert.Equal(PieceType.Rook,   type));
		Assert.All([blackKnight.Type, whiteKnight.Type], type => Assert.Equal(PieceType.Knight, type));
		Assert.All([blackBishop.Type, whiteBishop.Type], type => Assert.Equal(PieceType.Bishop, type));
		Assert.All([blackQueen.Type, whiteQueen.Type],   type => Assert.Equal(PieceType.Queen,  type));
		Assert.All([blackKing.Type, whiteKing.Type],     type => Assert.Equal(PieceType.King,   type));

		Assert.All(
			[blackPawn.Color, blackRook.Color, blackKnight.Color, blackBishop.Color, blackQueen.Color, blackKing.Color],
			color => Assert.Equal(PieceColor.Black, color));

		Assert.All(
			[whitePawn.Color, whiteRook.Color, whiteKnight.Color, whiteBishop.Color, whiteQueen.Color, whiteKing.Color],
			color => Assert.Equal(PieceColor.White, color));
	}
}
