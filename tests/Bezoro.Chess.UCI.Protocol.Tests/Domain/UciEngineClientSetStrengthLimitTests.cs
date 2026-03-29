using FluentAssertions;
using JetBrains.Annotations;
using NSubstitute;

namespace Bezoro.Chess.UCI.Protocol.Tests.Domain;

[TestSubject(typeof(UciEngineClient))]
public class UciEngineClientSetStrengthLimitTests
{
	[Fact]
	public async Task TryGetStrengthLimitRange_WhenStrengthOptionHasBounds_ShouldReturnAdvertisedRange()
	{
		var (transport, channel) = UciEngineClientTestHelpers.CreateMockTransport();
		var client = new UciEngineClient(transport);

		transport.When(x => x.WriteLineAsync("uci", Arg.Any<CancellationToken>()))
				 .Do(async _ =>
					 {
						 await channel.Writer.WriteAsync("option name UCI_LimitStrength type check default false");
						 await channel.Writer.WriteAsync("option name UCI_Elo type spin default 1320 min 1320 max 3190");
						 await channel.Writer.WriteAsync("uciok");
					 });

		transport.When(x => x.WriteLineAsync("isready", Arg.Any<CancellationToken>()))
				 .Do(_ => channel.Writer.TryWrite("readyok"));

		await client.StartAsync(CancellationToken.None);

		client.TryGetStrengthLimitRange(out int minElo, out int maxElo).Should().BeTrue();
		minElo.Should().Be(1320);
		maxElo.Should().Be(3190);

		await client.DisposeAsync();
	}

	[Fact]
	public async Task SetStrengthLimitAsync_WhenEloFallsOutsideAdvertisedRange_ShouldThrowArgumentOutOfRangeException()
	{
		var (transport, channel) = UciEngineClientTestHelpers.CreateMockTransport();
		var client = new UciEngineClient(transport);

		transport.When(x => x.WriteLineAsync("uci", Arg.Any<CancellationToken>()))
				 .Do(async _ =>
					 {
						 await channel.Writer.WriteAsync("option name UCI_LimitStrength type check default false");
						 await channel.Writer.WriteAsync("option name UCI_Elo type spin default 1320 min 1320 max 3190");
						 await channel.Writer.WriteAsync("uciok");
					 });

		transport.When(x => x.WriteLineAsync("isready", Arg.Any<CancellationToken>()))
				 .Do(_ => channel.Writer.TryWrite("readyok"));

		await client.StartAsync(CancellationToken.None);

		var act = () => client.SetStrengthLimitAsync(900, CancellationToken.None);

		await FluentActions.Awaiting(act)
						   .Should()
						   .ThrowAsync<ArgumentOutOfRangeException>();
		await transport.DidNotReceive()
					   .WriteLineAsync(Arg.Is<string>(line => line.StartsWith("setoption name UCI_", StringComparison.Ordinal)), Arg.Any<CancellationToken>());

		await client.DisposeAsync();
	}

	[Fact]
	public async Task SetStrengthLimitAsync_WhenRequiredStrengthOptionsAreMissing_ShouldThrowNotSupportedException()
	{
		var (transport, channel) = UciEngineClientTestHelpers.CreateMockTransport();
		var client = new UciEngineClient(transport);

		transport.When(x => x.WriteLineAsync("uci", Arg.Any<CancellationToken>()))
				 .Do(async _ =>
					 {
						 await channel.Writer.WriteAsync("option name Hash type spin default 16 min 1 max 4096");
						 await channel.Writer.WriteAsync("uciok");
					 });

		transport.When(x => x.WriteLineAsync("isready", Arg.Any<CancellationToken>()))
				 .Do(_ => channel.Writer.TryWrite("readyok"));

		await client.StartAsync(CancellationToken.None);

		var act = () => client.SetStrengthLimitAsync(1500, CancellationToken.None);

		await FluentActions.Awaiting(act)
						   .Should()
						   .ThrowAsync<NotSupportedException>();

		await client.DisposeAsync();
	}

	[Fact]
	public async Task SetStrengthLimitAsync_WhenStrengthOptionsAreAvailable_ShouldEnableStrengthLimitAndSetElo()
	{
		var (transport, channel) = UciEngineClientTestHelpers.CreateMockTransport();
		var client = new UciEngineClient(transport);

		transport.When(x => x.WriteLineAsync("uci", Arg.Any<CancellationToken>()))
				 .Do(async _ =>
					 {
						 await channel.Writer.WriteAsync("option name UCI_LimitStrength type check default false");
						 await channel.Writer.WriteAsync("option name UCI_Elo type spin default 1320 min 1320 max 3190");
						 await channel.Writer.WriteAsync("uciok");
					 });

		transport.When(x => x.WriteLineAsync("isready", Arg.Any<CancellationToken>()))
				 .Do(_ => channel.Writer.TryWrite("readyok"));

		await client.StartAsync(CancellationToken.None);
		transport.ClearReceivedCalls();

		await client.SetStrengthLimitAsync(1500, CancellationToken.None);

		Received.InOrder(
			async () =>
			{
				await transport.WriteLineAsync("setoption name UCI_LimitStrength value true", Arg.Any<CancellationToken>());
				await transport.WriteLineAsync("isready", Arg.Any<CancellationToken>());
				await transport.WriteLineAsync("setoption name UCI_Elo value 1500", Arg.Any<CancellationToken>());
				await transport.WriteLineAsync("isready", Arg.Any<CancellationToken>());
			});

		await client.DisposeAsync();
	}
}
