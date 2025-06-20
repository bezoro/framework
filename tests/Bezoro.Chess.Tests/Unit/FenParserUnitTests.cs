using Bezoro.Chess.API.Helpers;
using Bezoro.Chess.Domain.Helpers;
using Bezoro.Chess.Domain.Shared.Consts;
using Bezoro.Chess.Domain.Types.Records;
using Bezoro.Chess.Domain.Types.Structs;
using FluentAssertions;

namespace Bezoro.Chess.Tests.Unit;

public class FenParserUnitTests
{
	[Fact]
	public void Parse_StandardStartingPosition_ShouldReturnCorrectGameState()
	{
		// Arrange
		GameState expectedGameState = BoardSetup.CreateStandardGame();

		// Act
		GameState actualGameState = FenParser.FenToGameState(FenStrings.StandardStart);

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
