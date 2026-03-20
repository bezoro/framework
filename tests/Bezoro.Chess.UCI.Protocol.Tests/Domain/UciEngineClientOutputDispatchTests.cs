using System.Runtime.CompilerServices;
using Bezoro.Chess.UCI.Protocol.Tests.TestHelpers;
using FluentAssertions;
using JetBrains.Annotations;
using NSubstitute;

namespace Bezoro.Chess.UCI.Protocol.Tests.Domain;

[TestSubject(typeof(UciEngineClient))]
public class UciEngineClientOutputDispatchTests
{
	[Fact]
	public async Task Error_WhenReadLoopThrows_ShouldRaiseErrorEvent()
	{
		var transport      = Substitute.For<IUciTransport>();
		var client         = new UciEngineClient(transport);
		var errorTcs       = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
		var uciWritten     = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var isReadyWritten = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		transport.StartAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
		transport.StopAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
		transport.WriteLineAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
		transport.ReadLinesAsync(Arg.Any<CancellationToken>())
				 .Returns(call => FaultingHandshakeStream(
							  uciWritten.Task,
							  isReadyWritten.Task,
							  (CancellationToken)call[0]
						  )
				 );

		transport.When(x => x.WriteLineAsync("uci", Arg.Any<CancellationToken>()))
				 .Do(_ => uciWritten.TrySetResult());

		transport.When(x => x.WriteLineAsync("isready", Arg.Any<CancellationToken>()))
				 .Do(_ => isReadyWritten.TrySetResult());

		client.Error += ex => errorTcs.TrySetResult(ex);

		await client.StartAsync(CancellationToken.None);

		var forwarded = await errorTcs.Task.WaitAsync(TestConstants.DefaultTimeout);
		forwarded.Should().BeOfType<InvalidOperationException>();
		forwarded.Message.Should().Be("read boom");

		await client.DisposeAsync();
	}

	[Fact]
	public async Task Error_WhenTransportRaisesError_ShouldForwardException()
	{
		var (transport, _) = UciEngineClientTestHelpers.CreateMockTransport();
		var client   = new UciEngineClient(transport);
		var errorTcs = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);

		client.Error += ex => errorTcs.TrySetResult(ex);

		transport.Error += Raise.Event<Action<Exception>>(new InvalidOperationException("transport boom"));

		var forwarded = await errorTcs.Task.WaitAsync(TestConstants.DefaultTimeout);
		forwarded.Should().BeOfType<InvalidOperationException>();
		forwarded.Message.Should().Be("transport boom");
	}

	[Fact]
	public async Task GoAsync_WhenCanceled_ShouldSendStopBeforeReturningCancellation()
	{
		var (transport, channel) = UciEngineClientTestHelpers.CreateMockTransport();
		var client = await UciEngineClientTestHelpers.StartClientWithHandshakeAsync(transport, channel);

		transport.When(x => x.WriteLineAsync("stop", Arg.Any<CancellationToken>()))
				 .Do(_ => channel.Writer.TryWrite("bestmove e2e4 ponder e7e5"));

		using var cts    = new CancellationTokenSource();
		var       goTask = client.GoAsync(new() { Infinite = true }, cts.Token);

		await transport.Received().WriteLineAsync("go infinite", Arg.Any<CancellationToken>());
		await cts.CancelAsync();

		await FluentActions.Awaiting(() => goTask)
						   .Should()
						   .ThrowAsync<OperationCanceledException>();

		await transport.Received().WriteLineAsync("stop", Arg.Any<CancellationToken>());
		await client.DisposeAsync();
	}

	[Fact]
	public async Task GoAsync_WhenCompletedOutputIsMalformed_ShouldThrowInvalidOperationException()
	{
		var (transport, channel) = UciEngineClientTestHelpers.CreateMockTransport();
		var client = await UciEngineClientTestHelpers.StartClientWithHandshakeAsync(transport, channel);

		var goTask = client.GoAsync(new() { Depth = 4 }, CancellationToken.None);

		await channel.Writer.WriteAsync("info depth 4 seldepth 6 multipv 1 score cp 23 nodes 100 time 10 pv e2e4 e7e5");
		await channel.Writer.WriteAsync("bestmove ponder e7e5");

		await FluentActions.Awaiting(() => goTask)
						   .Should()
						   .ThrowAsync<InvalidOperationException>()
						   .WithMessage("*malformed*bestmove*");

		await client.DisposeAsync();
	}

	[Fact]
	public async Task GoAsync_WhenOutputSubscribersThrow_ShouldStillReturnSearchResult()
	{
		var (transport, channel) = UciEngineClientTestHelpers.CreateMockTransport();
		var client = await UciEngineClientTestHelpers.StartClientWithHandshakeAsync(transport, channel);

		client.InfoPvReceived   += _ => throw new InvalidOperationException("info boom");
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

	private static async IAsyncEnumerable<string> FaultingHandshakeStream(
		Task                                       uciWritten,
		Task                                       isReadyWritten,
		[EnumeratorCancellation] CancellationToken ct = default)
	{
		await uciWritten.WaitAsync(ct);
		yield return "uciok";

		await isReadyWritten.WaitAsync(ct);
		yield return "readyok";

		await Task.Delay(TestConstants.VeryShortDelay, ct);
		throw new InvalidOperationException("read boom");
	}
}
