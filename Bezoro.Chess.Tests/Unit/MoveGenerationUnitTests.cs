using Bezoro.Chess.ChessLogic;
using Bezoro.Chess.ChessLogic.Generators;
using FluentAssertions;
using Xunit.Abstractions;

namespace Bezoro.Chess.Tests.Unit;

public class MoveGenerationUnitTests(ITestOutputHelper output) : TestBase(output)
{
	[Fact]
	public void GenerateMoves_ForStandardStartingPosition_ShouldReturnCorrectNumberOfMoves()
	{
		var gameState = BoardSetup.CreateStandardGame();

		var moves = MoveGenerator.GenerateMoves(gameState).ToList();

		moves.Should().NotBeNullOrEmpty();
		moves.Should().HaveCount(20);
	}

	[Fact]
	public void GeneratePieceMoves_ForNullPiece_YieldsNoMoves()
	{
		var gameState = new GameState();

		var moves = MoveGenerator.GeneratePieceMoves(new("d4"), gameState);

		moves.Should().BeEmpty();
	}
}
