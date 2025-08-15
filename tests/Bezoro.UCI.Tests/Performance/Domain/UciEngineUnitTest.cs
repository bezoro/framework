using Bezoro.UCI.Domain;
using Bezoro.UCI.Tests.Unit.Domain;
using JetBrains.Annotations;

namespace Bezoro.UCI.Tests.Performance.Domain;

[TestSubject(typeof(UciEngine))]
public class UciEngineUnitTest : UciTestsBase
{
	[Fact]
	public async Task SendCommandAsync_WhenValidCommand_SendsCommandToEngineAndReturnsResponse()
	{
		await InitializeAsync();

		var response = await Engine.SendCommandAsync("uci", CancellationToken.None);

		Assert.NotNull(response.Completed);
		Assert.NotNull(response.Lines);
		Assert.Contains("uciok", await response.Completed);
		Assert.Contains("uciok", response.Lines);

		await DisposeAsync();
	}

	[Fact]
	public async Task WriteLineAsync_WhenValidCommand_SendsCommandToEngine()
	{
		await InitializeAsync();

		await Engine.WriteLineAsync("uci", CancellationToken.None);

		await DisposeAsync();
	}
}
