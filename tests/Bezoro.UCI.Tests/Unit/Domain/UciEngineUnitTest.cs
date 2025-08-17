using Bezoro.UCI.Domain;
using JetBrains.Annotations;

namespace Bezoro.UCI.Tests.Unit.Domain;

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
	public async Task WriteLineAsync_WhenValidCommand_SendsCommandToEngine()
	{
		await Engine.WriteLineSafeAsync("uci", CancellationToken.None);
	}
}
