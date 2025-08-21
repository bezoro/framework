using Bezoro.UCI.API.Types;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.UCI.Tests;

[TestSubject(typeof(MoveClassificationEngine))]
public class MoveClassificationEngineTests
{
	public const string STOCKFISH_PATH = "Engine/stockfish/stockfish-windows-x86-64-avx2.exe";

	[Fact]
	public async Task ClassifyAsync_WhenCalled_ReturnsClassifiedMovesStream()
	{
		await using var engine = new MoveClassificationEngine(STOCKFISH_PATH);
		await engine.StartAsync();

		var moveStream = engine.ClassifyAsync(Fen.Default, BoardState.FromFen(Fen.Default)!.Value);

		moveStream.Should().NotBeNull();
		await foreach (var move in moveStream)
		{
			move.Analysis.Should().NotBeNull();
			move.Move.Should().NotBeNull();
			move.Score.Should().NotBeNull();
		}
	}
}
