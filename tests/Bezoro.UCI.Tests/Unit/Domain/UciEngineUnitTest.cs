using Bezoro.UCI.Domain;
using Bezoro.UCI.Tests.Unit.Domain;
using JetBrains.Annotations;

namespace Bezoro.UCI.Tests.Performance.Domain;

[TestSubject(typeof(UciEngine))]
public class UciEngineUnitTest : UciTestsBase
{
	[Fact]
	public async Task GetCurrentFenAsync_WhenDefaultPosition_ReturnsExpectedFen()
	{
		var expectedFenString = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

		var fen = await Engine.GetCurrentFenAsync(CancellationToken.None);

		Assert.Equal(expectedFenString, fen.Raw);
	}

	[Fact]
	public async Task SendCommandAsync_WhenValidCommand_SendsCommandToEngineAndReturnsResponse()
	{
		var response = await Engine.SendCommandAsync("uci", CancellationToken.None);

		Assert.NotNull(response.Completed);
		Assert.NotNull(response.Lines);
		Assert.Contains("uciok", await response.Completed);
		Assert.Contains("uciok", response.Lines);
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
	public async Task WriteLineAsync_WhenValidCommand_SendsCommandToEngine()
	{
		await Engine.WriteLineAsync("uci", CancellationToken.None);
	}
}
