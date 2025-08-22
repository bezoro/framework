using System.Diagnostics;
using Bezoro.UCI.API.Types;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.UCI.Tests.Performance;

[TestSubject(typeof(MoveClassificationEngine))]
[Trait("Category", "Performance")]
public class MoveClassificationEnginePerformanceTests
{
	public const string STOCKFISH_PATH = "Engine/stockfish/stockfish-windows-x86-64-avx2.exe";

	[Fact]
	public async Task ClassifyAsync_Performance()
	{
		var fen   = Fen.Default;
		var board = BoardState.FromFen(fen)!.Value;

		await using var engine = new MoveClassificationEngine(STOCKFISH_PATH);
		await engine.StartAsync();

		var moveStream = engine.ClassifyAsync(fen, board);

		moveStream.Should().NotBeNull();
		var stopWatch = new Stopwatch();
		stopWatch.Start();
		await foreach (var _ in moveStream) { }

		stopWatch.Stop();
		stopWatch.ElapsedMilliseconds.Should().BeLessThan(5000);
	}
}
