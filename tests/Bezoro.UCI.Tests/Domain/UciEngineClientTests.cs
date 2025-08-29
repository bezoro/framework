using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Bezoro.UCI.API.Types;
using Bezoro.UCI.Domain;
using JetBrains.Annotations;
using NSubstitute;

namespace Bezoro.UCI.Tests.Domain;

[TestSubject(typeof(UciEngineClient))]
public class UciEngineClientTests
{
	[Fact(Timeout = 4000)]
	public async Task GetFenViaDAsync_FenOnlyAndFenPlusCheckers_AndCancellation()
	{
		var transport = Substitute.For<IUciTransport>();
		transport.StartAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
		transport.StopAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
		transport.WriteLineAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
		var ch = Channel.CreateUnbounded<string>();
		transport.ReadLinesAsync(Arg.Any<CancellationToken>())
				 .Returns(ci => StreamFromChannel(ch.Reader, (CancellationToken)ci[0]));

		var client = new UciEngineClient(transport);

		// Start with a handshake pump to avoid races
		using var pumpCts = new CancellationTokenSource();
		var pump = Task.Run(
			async () =>
			{
				for (var i = 0; i < 50 && !pumpCts.IsCancellationRequested; i++)
				{
					await ch.Writer.WriteAsync("uciok");
					await Task.Delay(5);
					await ch.Writer.WriteAsync("readyok");
					await Task.Delay(5);
				}
			},
			pumpCts.Token);

		await client.StartAsync(CancellationToken.None);
		pumpCts.Cancel();
		await pump;

		// Case B: fen + checkers quickly (emit upon 'd')
		string fen = Fen.Default.Raw;
		transport.ClearReceivedCalls();
		transport.When(x => x.WriteLineAsync("d", Arg.Any<CancellationToken>()))
				 .Do(async _ =>
				 {
					 await ch.Writer.WriteAsync($"fen: {fen}");
					 await ch.Writer.WriteAsync("checkers: e1");
				 });

		var fenTask   = client.GetFenViaDAsync(CancellationToken.None);
		var fenResult = await fenTask;
		Assert.NotNull(fenResult);
		Assert.Equal(fen,  fenResult!.Value.Raw);
		Assert.Equal("e1", fenResult!.Value.Checkers);
	}

	[Fact(Timeout = 3000)]
	public async Task GetLegalMovesViaGoPerft1Async_ParsesMovesAndWaitsReadyOk()
	{
		var transport = Substitute.For<IUciTransport>();
		transport.StartAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
		transport.StopAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
		transport.WriteLineAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
		var ch = Channel.CreateUnbounded<string>();
		transport.ReadLinesAsync(Arg.Any<CancellationToken>())
				 .Returns(ci => StreamFromChannel(ch.Reader, (CancellationToken)ci[0]));

		var client = new UciEngineClient(transport);

		// Start with a handshake pump to avoid races
		using var pumpCts = new CancellationTokenSource();
		var pump = Task.Run(
			async () =>
			{
				for (var i = 0; i < 50 && !pumpCts.IsCancellationRequested; i++)
				{
					await ch.Writer.WriteAsync("uciok");
					await Task.Delay(5);
					await ch.Writer.WriteAsync("readyok");
					await Task.Delay(5);
				}
			},
			pumpCts.Token);

		await client.StartAsync(CancellationToken.None);
		pumpCts.Cancel();
		await pump;

		// Trigger perft and feed lines with mixed tokens; gate readyok until after we write tokens
		var readyGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		transport.ClearReceivedCalls();
		transport.When(x => x.WriteLineAsync("isready", Arg.Any<CancellationToken>()))
				 .Do(async _ =>
				 {
					 await readyGate.Task;
					 await ch.Writer.WriteAsync("readyok");
				 });

		var perftTask = client.GetLegalMovesViaGoPerft1Async(CancellationToken.None);
		await ch.Writer.WriteAsync("e2e4, e7e5 ; bad | a7a8q : h7h8N");
		await Task.Delay(50);
		readyGate.TrySetResult();
		var moves = await perftTask;

		Assert.Contains("e2e4",  moves);
		Assert.Contains("e7e5",  moves);
		Assert.Contains("a7a8q", moves);
		Assert.Contains("h7h8N", moves);
		Assert.DoesNotContain("bad", moves);
		await transport.Received().WriteLineAsync("go perft 1", Arg.Any<CancellationToken>());
	}

	[Fact(Timeout = 10000)]
	public async Task GoAsync_FastBestmove_TimeoutWithGrace_AndCancellation()
	{
		var transport = Substitute.For<IUciTransport>();
		transport.StartAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
		transport.StopAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
		transport.WriteLineAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
		var ch = Channel.CreateUnbounded<string>();
		transport.ReadLinesAsync(Arg.Any<CancellationToken>())
				 .Returns(ci => StreamFromChannel(ch.Reader, (CancellationToken)ci[0]));

		var client = new UciEngineClient(transport);

		// Hook handshake
		transport.When(x => x.WriteLineAsync("uci", Arg.Any<CancellationToken>()))
				 .Do(async _ => await ch.Writer.WriteAsync("uciok"));

		transport.When(x => x.WriteLineAsync("isready", Arg.Any<CancellationToken>()))
				 .Do(async _ => await ch.Writer.WriteAsync("readyok"));

		await client.StartAsync(CancellationToken.None);

		// Case 1: bestmove arrives promptly (emit upon 'go depth 8')
		transport.ClearReceivedCalls();
		transport.When(x => x.WriteLineAsync(
						   Arg.Is<string>(s => s.StartsWith("go depth 8")),
						   Arg.Any<CancellationToken>()))
				 .Do(async _ =>
				 {
					 await ch.Writer.WriteAsync(
						 "info depth 8 seldepth 10 multipv 1 score cp 34 nodes 100 nps 1000 tbhits 0 time 5 pv e2e4 e7e5");

					 await ch.Writer.WriteAsync("bestmove e2e4 ponder e7e5");
				 });

		var goTask1 = client.GoAsync(new() { Depth = 8 }, CancellationToken.None);
		var res1    = await goTask1;
		Assert.Equal("e2e4",              res1.BestMove);
		Assert.Equal("e7e5",              res1.PonderMove);
		Assert.Equal(EngineActivity.Idle, client.Activity);

		// Case 2: timeout then grace where bestmove arrives after 'stop'
		transport.ClearReceivedCalls();
		transport.When(x => x.WriteLineAsync("stop", Arg.Any<CancellationToken>()))
				 .Do(async _ => { await ch.Writer.WriteAsync("bestmove g1f3"); });

		var goTask2 = client.GoAsync(new() { MoveTimeMs = 10 }, CancellationToken.None);
		var res2    = await goTask2;
		Assert.Equal("g1f3", res2.BestMove);
		await transport.Received().WriteLineAsync("stop", CancellationToken.None);

		// Case 3: cancellation without bestmove
		using var cts = new CancellationTokenSource(50);
		await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await client.GoAsync(
																				new() { Depth = 12 },
																				cts.Token));

		await client.DisposeAsync();
	}

	[Fact]
	public async Task GoFireAndForgetAsync_WritesCommandAndSetsActivity()
	{
		var transport = Substitute.For<IUciTransport>();
		transport.WriteLineAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
		var client = new UciEngineClient(transport);

		await client.GoFireAndForgetAsync(new(), CancellationToken.None);
		await transport.Received().WriteLineAsync("go depth 6", CancellationToken.None);
		Assert.Equal(EngineActivity.Searching, client.Activity);

		await client.GoFireAndForgetAsync(new() { Ponder = true }, CancellationToken.None);
		await transport.Received().WriteLineAsync("go ponder depth 6", CancellationToken.None);
		Assert.Equal(EngineActivity.Pondering, client.Activity);
	}

	[Fact(Timeout = 1500)]
	public async Task IsReadyAsync_RespectsCancellationToken()
	{
		// Arrange
		var transport = Substitute.For<IUciTransport>();
		transport.WriteLineAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
		var       client = new UciEngineClient(transport);
		using var cts    = new CancellationTokenSource(50);

		// Act + Assert
		await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => { await client.IsReadyAsync(cts.Token); });
	}

	[Fact]
	public async Task SetOptionAsync_WithoutValue_SendsCorrectUciCommand()
	{
		// Arrange
		var transport = Substitute.For<IUciTransport>();
		transport.WriteLineAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
		var client = new UciEngineClient(transport);
		var ct     = new CancellationTokenSource().Token;

		// Act
		await client.SetOptionAsync("Ponder", null, ct);

		// Assert
		await transport.Received(1).WriteLineAsync("setoption name Ponder", ct);
	}

	[Fact]
	public async Task SetOptionAsync_WithValue_SendsCorrectUciCommand()
	{
		// Arrange
		var transport = Substitute.For<IUciTransport>();
		transport.WriteLineAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
		var client = new UciEngineClient(transport);
		var ct     = new CancellationTokenSource().Token;

		// Act
		await client.SetOptionAsync("Hash", "256", ct);

		// Assert
		await transport.Received(1).WriteLineAsync("setoption name Hash value 256", ct);
	}

	[Fact]
	public async Task SetPositionAsync_ValidAndInvalid()
	{
		var transport = Substitute.For<IUciTransport>();
		transport.WriteLineAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
		var client = new UciEngineClient(transport);
		var ct     = CancellationToken.None;

		// invalid fen -> throws
		await Assert.ThrowsAsync<ArgumentException>(async () => await client.SetPositionAsync(Fen.Empty(), null, ct));

		// valid fen without moves
		var fen = Fen.Default;
		await client.SetPositionAsync(fen, null, ct);
		await transport.Received().WriteLineAsync(Arg.Is<string>(s => s.StartsWith($"position fen {fen.Raw}")), ct);

		// valid fen with moves
		await client.SetPositionAsync(fen, new[] { "e2e4", "e7e5" }, ct);
		await transport.Received().WriteLineAsync(Arg.Is<string>(s => s.Contains("moves e2e4 e7e5")), ct);
	}

	[Fact(Timeout = 4000)]
	public async Task StartAsync_InitializesAndBecomesIdle()
	{
		var transport = Substitute.For<IUciTransport>();
		transport.StartAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
		transport.StopAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
		transport.WriteLineAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

		var ch = Channel.CreateUnbounded<string>();
		transport.ReadLinesAsync(Arg.Any<CancellationToken>())
				 .Returns(ci => StreamFromChannel(ch.Reader, (CancellationToken)ci[0]));

		var client = new UciEngineClient(transport);

		// Pump handshake lines in background to avoid races
		var pumpCts = new CancellationTokenSource();
		var pump = Task.Run(
			async () =>
			{
				for (var i = 0; i < 20 && !pumpCts.IsCancellationRequested; i++)
				{
					await ch.Writer.WriteAsync("uciok");
					await Task.Delay(10);
					await ch.Writer.WriteAsync("readyok");
					await Task.Delay(10);
				}
			},
			pumpCts.Token);

		await client.StartAsync(CancellationToken.None);
		pumpCts.Cancel();
		await pump;

		// Validations
		await transport.Received().WriteLineAsync("uci",     Arg.Any<CancellationToken>());
		await transport.Received().WriteLineAsync("isready", Arg.Any<CancellationToken>());
		Assert.Equal(EngineActivity.Idle, client.Activity);

		await client.DisposeAsync();
	}

	[Fact(Timeout = 1500)]
	public async Task UciInitAsync_RespectsCancellationToken()
	{
		// Arrange
		var transport = Substitute.For<IUciTransport>();
		transport.WriteLineAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
		var       client = new UciEngineClient(transport);
		using var cts    = new CancellationTokenSource(50);

		// Act + Assert
		await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => { await client.UciInitAsync(cts.Token); });
	}

	[Fact]
	public void BuildGoCommand_CombinationsAndDefaults()
	{
		// default depth added when no limits
		string cmd1 = UciEngineClient.BuildGoCommand(new());
		Assert.Equal("go depth 6", cmd1);

		// ponder + infinite flags
		string cmd2 = UciEngineClient.BuildGoCommand(new() { Ponder = true, Infinite = true });
		Assert.Equal("go ponder infinite", cmd2);

		// time controls
		string cmd3 = UciEngineClient.BuildGoCommand(
			new() { WhiteTimeMs = 1000, BlackTimeMs = 2000, WhiteIncrementMs = 10, BlackIncrementMs = 20 });

		Assert.Equal("go wtime 1000 btime 2000 winc 10 binc 20", cmd3);

		// nodes/depth/mate
		string cmd4 = UciEngineClient.BuildGoCommand(new() { Nodes = 123, Depth = 7, Mate = 2 });
		Assert.Equal("go nodes 123 depth 7 mate 2", cmd4);

		// searchmoves filters and lowercases
		string cmd5 = UciEngineClient.BuildGoCommand(
			new() { SearchMoves = new[] { "E2E4", "bad", "a7a8Q", "" } });

		Assert.Equal("go depth 6 searchmoves e2e4 a7a8q", cmd5);
	}

	[Fact]
	public void IsUciMoveString_ValidAndInvalid()
	{
		Assert.True(UciEngineClient.IsUciMoveString("e2e4"));
		Assert.True(UciEngineClient.IsUciMoveString("a7a8q"));
		Assert.True(UciEngineClient.IsUciMoveString("H7H8N"));
		Assert.False(UciEngineClient.IsUciMoveString("e9e4"));
		Assert.False(UciEngineClient.IsUciMoveString("i2e4"));
		Assert.False(UciEngineClient.IsUciMoveString("e2e"));
		Assert.False(UciEngineClient.IsUciMoveString("e2e45"));
		Assert.False(UciEngineClient.IsUciMoveString("e2e4x"));
	}

	private static async IAsyncEnumerable<string> StreamFromChannel(
		ChannelReader<string>                      reader,
		[EnumeratorCancellation] CancellationToken ct = default)
	{
		while (!ct.IsCancellationRequested)
		{
			while (reader.TryRead(out string? item))
				yield return item;

			bool canRead = await reader.WaitToReadAsync(ct).ConfigureAwait(false);
			if (!canRead) yield break;
		}
	}
}
