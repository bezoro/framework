using Bezoro.Chess.Domain.Helpers;
using Bezoro.Chess.Domain.Shared.Consts;
using Bezoro.Chess.Domain.Shared.Enums;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.Chess.Tests.Domain.Unit;

[TestSubject(typeof(FenParser))]
public class FenParserUnitTests
{
	[Fact]
	public void Parse_StandardStartingPosition_ShouldReturnCorrectGameState()
	{
		// Arrange
		var expectedGameState = BoardSetup.CreateStandardGame();

		// Act
		var actualGameState = FenParser.FenToGameState(FenStrings.StandardStart);

		// Assert
		actualGameState.Board.Should().BeEquivalentTo(expectedGameState.Board);
		actualGameState.ActiveColor.Should().Be(PieceColor.White);
		actualGameState.Castling.Should().Be(
			CastlingRights.WhiteKingside  |
			CastlingRights.WhiteQueenside |
			CastlingRights.BlackKingside  |
			CastlingRights.BlackQueenside);

		actualGameState.EnPassantTargetSquare.IsValid.Should().Be(false);
	}
}
