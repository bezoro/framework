using Bezoro.Chess.ChessLogic;
using FluentAssertions;

namespace Bezoro.Chess.Tests.Unit;

public class FenParserUnitTests
{
	[Fact]
	public void Parse_StandardStartingPosition_ShouldReturnCorrectGameState()
	{
		// Arrange
		var expectedGameState = BoardSetup.CreateStandardGame();

		// Act
		var actualGameState = FenParser.Parse(FenStrings.StandardStart);

		// Assert
		actualGameState.PiecePositions.Should().BeEquivalentTo(expectedGameState.PiecePositions);
		actualGameState.ActiveColor.Should().Be(PieceColor.White);
		actualGameState.Castling.Should().Be(
			CastlingRights.WhiteKingside  |
			CastlingRights.WhiteQueenside |
			CastlingRights.BlackKingside  |
			CastlingRights.BlackQueenside);

		actualGameState.EnPassantTargetSquare.Should().BeNull();
	}
}
