using Bezoro.Chess.UCI.Protocol.Tests.TestHelpers;
using FluentAssertions;

namespace Bezoro.Chess.UCI.Protocol.Tests.Domain;

[Trait("Category", "Integration")]
[Collection("Stockfish")]
public class UciWdlIntegrationTests
{
	[Fact]
	public async Task StockfishSearch_ShouldExposeValidWdlForTheFinalPrimaryVariation()
	{
		await using var client = new UciEngineClient(TestResourcePaths.STOCKFISH_PATH);
		await client.StartAsync();
		client.TryGetOption("UCI_ShowWDL", out _).Should().BeTrue();
		await client.SetOptionAsync("UCI_ShowWDL", "true", default);
		await client.SetPositionAsync(Fen.Default, null, default);
		var result = await client.GoAsync(new() { Depth = 6 }, default);
		result.BestPv.Should().NotBeNull();
		result.BestPv!.Value.Wdl.Should().NotBeNull();
		result.BestPv.Value.Wdl!.Value.IsValid.Should().BeTrue();
		result.BestPv.Value.Wdl.Value.ExpectedPoints.Should().BeInRange(0, 1);
		result.BestPv.Value.ScoreBound.Should().Be(UciScoreBound.Exact);
	}
}
