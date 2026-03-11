using Bezoro.UCI.Domain;
using Bezoro.UCI.Tests.TestHelpers;
using FluentAssertions;
using JetBrains.Annotations;
using NSubstitute;

namespace Bezoro.UCI.Tests.Domain;

[TestSubject(typeof(UciEngineClient))]
public class UciEngineClientOutputDispatchTests
{
	[Fact]
	public async Task GoAsync_WhenOutputSubscribersThrow_ShouldStillReturnSearchResult()
	{
		var (transport, channel) = UciEngineClientTestHelpers.CreateMockTransport();
		var client = await UciEngineClientTestHelpers.StartClientWithHandshakeAsync(transport, channel);

		client.InfoPvReceived += _ => throw new InvalidOperationException("info boom");
		client.BestMoveReceived += (_, _) => throw new InvalidOperationException("bestmove boom");

		var goTask = client.GoAsync(new() { Depth = 4 }, CancellationToken.None);

		await channel.Writer.WriteAsync("info depth 4 seldepth 6 multipv 1 score cp 23 nodes 100 time 10 pv e2e4 e7e5");
		await channel.Writer.WriteAsync("bestmove e2e4 ponder e7e5");

		var result = await goTask.WaitAsync(TestConstants.DefaultTimeout);

		result.BestMove.Should().Be("e2e4");
		result.PrincipalVariations.Should().ContainSingle();

		await client.DisposeAsync();
	}

	[Fact]
	public async Task GoAsync_WhenCanceled_ShouldSendStopBeforeReturningCancellation()
	{
		var (transport, channel) = UciEngineClientTestHelpers.CreateMockTransport();
		var client = await UciEngineClientTestHelpers.StartClientWithHandshakeAsync(transport, channel);

		transport.When(x => x.WriteLineAsync("stop", Arg.Any<CancellationToken>()))
				 .Do(_ => channel.Writer.TryWrite("bestmove e2e4 ponder e7e5"));

		using var cts = new CancellationTokenSource();
		var goTask = client.GoAsync(new() { Infinite = true }, cts.Token);

		await transport.Received().WriteLineAsync("go infinite", Arg.Any<CancellationToken>());
		await cts.CancelAsync();

		await FluentActions.Awaiting(() => goTask)
						   .Should()
						   .ThrowAsync<OperationCanceledException>();

		await transport.Received().WriteLineAsync("stop", Arg.Any<CancellationToken>());
		await client.DisposeAsync();
	}

	[Fact]
	public async Task StartAsync_WhenLineReceivedSubscriberThrows_ShouldStillCompleteHandshake()
	{
		var (transport, channel) = UciEngineClientTestHelpers.CreateMockTransport();
		var client = new UciEngineClient(transport);

		transport.When(x => x.WriteLineAsync("uci", Arg.Any<CancellationToken>()))
				 .Do(_ => channel.Writer.TryWrite("uciok"));
		transport.When(x => x.WriteLineAsync("isready", Arg.Any<CancellationToken>()))
				 .Do(_ => channel.Writer.TryWrite("readyok"));

		client.LineReceived += _ => throw new InvalidOperationException("line boom");

		var startTask = client.StartAsync(CancellationToken.None);

		await startTask.WaitAsync(TestConstants.DefaultTimeout);

		client.Activity.Should().Be(EngineActivity.Idle);

		await client.DisposeAsync();
	}
}
