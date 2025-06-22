using System.Diagnostics;
using Bezoro.Chess.Domain.Functions.Moves;
using Bezoro.Chess.Domain.Types.Records;
using Bezoro.Chess.Domain.Types.Structs;
using Bezoro.Chess.Tests.Unit;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit.Abstractions;

namespace Bezoro.Chess.Tests.Domain.Performance;

[TestSubject(typeof(MoveGenerator))]
public class MoveGeneratorPerformanceTests : TestBase
{
	public MoveGeneratorPerformanceTests(ITestOutputHelper output) : base(output) { }

	[Fact]
	public void GenerateMoves_WhenOneHundredMillionGenerations_ShouldbeFast()
	{
		// Arrange
		var       gameState = GameState.CreateInitial(); // initial (default) board position
		const int Runs      = 1_000_000;
		var       stopwatch = Stopwatch.StartNew();

		// Act
		for (var i = 0 ; i < Runs ; i++)
		{
			foreach (Move _ in MoveGenerator.GenerateMoves(gameState))
			{
				// do nothing – we just want to force enumeration
			}
		}

		stopwatch.Stop();

		// Assert – keep a generous upper-bound so the build server remains green
		stopwatch.Elapsed
				 .Should()
				 .BeLessThan(
					 TimeSpan.FromSeconds(2),
					 $"generating moves {Runs:N0} times should be reasonably fast (elapsed: {stopwatch.Elapsed})");

		Output.WriteLine(stopwatch.Elapsed.ToString());
	}
}
