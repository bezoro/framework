using Bezoro.Chess.Domain.Helpers;
using Bezoro.Chess.Domain.Shared.Consts;
using Bezoro.Chess.Domain.Shared.Enums;
using Bezoro.Chess.Domain.Types.Records;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.Chess.Tests.Unit;

[TestSubject(typeof(FenParser))]
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
