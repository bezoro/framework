using Bezoro.UCI.Domain;
using Bezoro.UCI.Tests.Unit.Domain;
using JetBrains.Annotations;

namespace Bezoro.UCI.Tests.Integration.Domain;

[TestSubject(typeof(UciEngine))]
public class UciEngineIntegrationTest : UciTestsBase
{
	[Fact]
	public async Task CalculateScoreForMoveAsync_WhenLegalMove_ReturnsScoredMove()
	{
		var move = await Engine.CalculateScoreForMoveAsync("e2e4", CancellationToken.None);
		Assert.Multiple(() =>
		{
			Assert.NotNull(move.ScoreCp);
			Assert.NotEqual(0, move.ScoreCp);
			Assert.Null(move.ScoreMate);
		});
	}

	[Fact]
	public async Task SetPositionAsync_WhenValidFenString_SetsTheEngineRootPosition()
	{
		var nonStandardFen  = "r3k2r/p1ppqpb1/bn2pnp1/3PN3/1p2P3/2N2Q1p/PPPBBPPP/R3K2R w KQkq - 0 1";
		var positionCommand = new PositionCommand(nonStandardFen);

		await Engine.SetPositionAsync(positionCommand, CancellationToken.None);

		var currentPosition = await Engine.GetCurrentFenAsync(CancellationToken.None);
		Assert.Equal(nonStandardFen, currentPosition.Raw);
	}

	[Fact]
	public async Task TryGetMoveScoreFromHistory_WhenMoveExistsInHistory_GetsTheMove()
	{
		await Engine.StartSearchForSecondsAsync(1, CancellationToken.None);
		var move = Engine.TryGetMoveScoreFromHistory("e2e4");

		Assert.Multiple(() =>
		{
			Assert.NotNull(move);
			Assert.NotNull(move.Value.ScoreCp);
			Assert.NotEqual(0, move.Value.ScoreCp);
			Assert.Null(move.Value.ScoreMate);
		});
	}
}
