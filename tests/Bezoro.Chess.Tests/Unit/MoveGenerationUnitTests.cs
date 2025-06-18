using Bezoro.Chess.Domain;
using Bezoro.Chess.Domain.Board;
using Bezoro.Chess.Domain.Moves;
using FluentAssertions;
using Xunit.Abstractions;

namespace Bezoro.Chess.Tests.Unit;

public class MoveGenerationUnitTests(ITestOutputHelper output) : TestBase(output)
{
	[Fact]
	public void GenerateMoves_ForStandardStartingPosition_ShouldReturnCorrectNumberOfMoves()
	{
		GameState gameState = BoardSetup.CreateStandardGame();

		List<Move> moves = MoveGenerator.GenerateMoves(gameState).ToList();

		moves.Should().NotBeNullOrEmpty();
		moves.Should().HaveCount(20);
	}

	[Fact]
	public void GeneratePieceMoves_ForNullPiece_YieldsNoMoves()
	{
		var gameState = new GameState();

		IEnumerable<Move> moves = MoveGenerator.GeneratePieceMoves(new("d4"), gameState);

		moves.Should().BeEmpty();
	}
}
