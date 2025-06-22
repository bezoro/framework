using Bezoro.Chess.Domain.Functions.Moves;
using Bezoro.Chess.Domain.Helpers;
using Bezoro.Chess.Domain.Types.Records;
using Bezoro.Chess.Domain.Types.Structs;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit.Abstractions;

namespace Bezoro.Chess.Tests.Unit;

[TestSubject(typeof(MoveGenerator))]
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

		IEnumerable<Move> moves = MoveGenerator.GeneratePieceMoves(new Position("d4"), gameState);

		moves.Should().BeEmpty();
	}
}
