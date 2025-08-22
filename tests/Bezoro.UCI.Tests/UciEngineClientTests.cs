using Bezoro.UCI.API.Types;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.UCI.Tests;

[TestSubject(typeof(UciEngineClient))]
public class UciEngineClientTests
{
	public const string STOCKFISH_PATH = "Engine/stockfish/stockfish-windows-x86-64-avx2.exe";

	[Fact]
	public async Task GoAsync_WithDepth_SearchesWithExpectedDepth()
	{
		uint expectedDepth = 6;
		var  transport     = new ProcessUciTransport(STOCKFISH_PATH);
		var  engine        = new UciEngineClient(transport);
		await engine.StartAsync();

		var searchResult = await engine.GoAsync(new() { Depth = expectedDepth }, CancellationToken.None);

		searchResult.Should().NotBeNull();
		searchResult.ReachedDepth.Should().Be(expectedDepth);
	}

	[Fact]
	public async Task GoAsync_WithSearchMove_SearchesWithExpectedSearchMove()
	{
		var transport = new ProcessUciTransport(STOCKFISH_PATH);
		var engine    = new UciEngineClient(transport);
		await engine.StartAsync();
		await engine.SetPositionAsync(Fen.Default, null, CancellationToken.None);

		var searchResult = await engine.GoAsync(new() { SearchMoves = ["a2a4"] }, CancellationToken.None);

		searchResult.Should().NotBeNull();
		searchResult.PrincipalVariations.Count.Should().BeGreaterThan(0);
	}
}
