using Bezoro.Chess.Domain.Functions.Moves;
using Bezoro.Chess.Domain.Helpers;
using Bezoro.Chess.Domain.Types.Records;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit.Abstractions;

namespace Bezoro.Chess.Tests.Domain.Unit.MoveGeneration;

[TestSubject(typeof(MoveGenerator))]
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
