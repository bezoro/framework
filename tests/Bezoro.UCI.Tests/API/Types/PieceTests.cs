using Bezoro.UCI.API.Common.Enums;
using Bezoro.UCI.API.Types;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.UCI.Tests.API.Types;

[TestSubject(typeof(Piece))]
public class PieceTests
{
	[Fact]
	public void FromChar_WhenValidParameters_ReturnsExpectedPiece()
	{
		// Arrange & Act
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

		// Assert - Piece types
		blackPawn.Type.Should().Be(PieceType.Pawn);
		whitePawn.Type.Should().Be(PieceType.Pawn);
		blackRook.Type.Should().Be(PieceType.Rook);
		whiteRook.Type.Should().Be(PieceType.Rook);
		blackKnight.Type.Should().Be(PieceType.Knight);
		whiteKnight.Type.Should().Be(PieceType.Knight);
		blackBishop.Type.Should().Be(PieceType.Bishop);
		whiteBishop.Type.Should().Be(PieceType.Bishop);
		blackQueen.Type.Should().Be(PieceType.Queen);
		whiteQueen.Type.Should().Be(PieceType.Queen);
		blackKing.Type.Should().Be(PieceType.King);
		whiteKing.Type.Should().Be(PieceType.King);

		// Assert - Black piece colors
		blackPawn.Color.Should().Be(PieceColor.Black);
		blackRook.Color.Should().Be(PieceColor.Black);
		blackKnight.Color.Should().Be(PieceColor.Black);
		blackBishop.Color.Should().Be(PieceColor.Black);
		blackQueen.Color.Should().Be(PieceColor.Black);
		blackKing.Color.Should().Be(PieceColor.Black);

		// Assert - White piece colors
		whitePawn.Color.Should().Be(PieceColor.White);
		whiteRook.Color.Should().Be(PieceColor.White);
		whiteKnight.Color.Should().Be(PieceColor.White);
		whiteBishop.Color.Should().Be(PieceColor.White);
		whiteQueen.Color.Should().Be(PieceColor.White);
		whiteKing.Color.Should().Be(PieceColor.White);
	}
}
