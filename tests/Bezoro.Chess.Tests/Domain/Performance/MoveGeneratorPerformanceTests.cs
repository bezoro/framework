using System.Diagnostics;
using Bezoro.Chess.Domain.Functions.Moves;
using Bezoro.Chess.Domain.Types.Records;
using Bezoro.Chess.Domain.Types.Structs;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.Chess.Tests.Domain.Performance;

[TestSubject(typeof(MoveGenerator))]
public class MoveGeneratorPerformanceTests
{
	/* Implement this function */
	[Fact]
	public void GenerateMoves_WhenOneHundredMillionGenerations_ShouldbeFast()
	{
		// Arrange
		var       gameState = GameState.CreateInitial(); // initial (default) board position
		const int runs      = 100_000_000;
		var       stopwatch = Stopwatch.StartNew();

		// Act
		for (var i = 0 ; i < runs ; i++)
		{
			IEnumerable<Move> moves = MoveGenerator.GenerateMoves(gameState);
		}

		stopwatch.Stop();

		// Assert – keep a generous upper-bound so the build server remains green
		stopwatch.Elapsed
				 .Should()
				 .BeLessThan(
					 TimeSpan.FromSeconds(2),
					 $"generating moves {runs:N0} times should be reasonably fast (elapsed: {stopwatch.Elapsed})");
	}
}
