using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Bezoro.UCI.API.Types;
using Bezoro.UCI.Domain;
using Bezoro.UCI.Tests.TestHelpers;
using FluentAssertions;
using JetBrains.Annotations;
using NSubstitute;

namespace Bezoro.UCI.Tests.Domain;

[TestSubject(typeof(UciEngineClient))]
public class UciEngineClientTests
{
	public class Unit
	{
		[Fact]
		public void BuildGoCommand_CombinationsAndDefaults()
		{
			// Arrange & Act
			// default depth added when no limits
			string cmd1 = UciEngineClient.BuildGoCommand(new());
			cmd1.Should().Be("go depth 6", "default depth should be 6");

			// ponder + infinite flags
			string cmd2 = UciEngineClient.BuildGoCommand(new() { Ponder = true, Infinite = true });
			cmd2.Should().Be("go ponder infinite", "ponder and infinite flags should be combined");

			// time controls
			string cmd3 = UciEngineClient.BuildGoCommand(
				new() { WhiteTimeMs = 1000, BlackTimeMs = 2000, WhiteIncrementMs = 10, BlackIncrementMs = 20 });
			cmd3.Should().Be("go wtime 1000 btime 2000 winc 10 binc 20", "time controls should be formatted correctly");

			// nodes/depth/mate
			string cmd4 = UciEngineClient.BuildGoCommand(new() { Nodes = 123, Depth = 7, Mate = 2 });
			cmd4.Should().Be("go nodes 123 depth 7 mate 2", "nodes, depth, and mate should be included");

			// searchmoves filters and lowercases
			string cmd5 = UciEngineClient.BuildGoCommand(
				new() { SearchMoves = new[] { "E2E4", "bad", "a7a8Q", "" } });
			cmd5.Should().Be("go depth 6 searchmoves e2e4 a7a8q", "searchmoves should be filtered and lowercased");
		}

		[Fact]
		public void IsUciMoveString_ValidAndInvalid()
		{
			// Valid moves
			UciEngineClient.IsUciMoveString("e2e4").Should().BeTrue("e2e4 is a valid UCI move");
			UciEngineClient.IsUciMoveString("a7a8q").Should().BeTrue("a7a8q is a valid promotion move");
			UciEngineClient.IsUciMoveString("H7H8N").Should().BeTrue("H7H8N is a valid move (case insensitive)");

			// Invalid moves
			UciEngineClient.IsUciMoveString("e9e4").Should().BeFalse("e9 is an invalid square");
			UciEngineClient.IsUciMoveString("i2e4").Should().BeFalse("i2 is an invalid square");
			UciEngineClient.IsUciMoveString("e2e").Should().BeFalse("incomplete move notation");
			UciEngineClient.IsUciMoveString("e2e45").Should().BeFalse("rank out of bounds");
			UciEngineClient.IsUciMoveString("e2e4x").Should().BeFalse("extra characters");
		}

		[Fact]
		public async Task GoFireAndForgetAsync_WritesCommandAndSetsActivity()
		{
			// Arrange
			var transport = Substitute.For<IUciTransport>();
			transport.WriteLineAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
			var client = new UciEngineClient(transport);

			// Act
			await client.GoFireAndForgetAsync(new(), CancellationToken.None);

			// Assert
			await transport.Received().WriteLineAsync("go depth 6", CancellationToken.None);
			client.Activity.Should().Be(EngineActivity.Searching, "activity should be Searching after go command");

			// Act - with ponder
			await client.GoFireAndForgetAsync(new() { Ponder = true }, CancellationToken.None);

			// Assert
			await transport.Received().WriteLineAsync("go ponder depth 6", CancellationToken.None);
			client.Activity.Should().Be(EngineActivity.Pondering, "activity should be Pondering when ponder is enabled");
		}

		[Fact]
		public async Task SetOptionAsync_ValueContainingSpaces_SendsVerbatimValue()
		{
			// Arrange
			var transport = Substitute.For<IUciTransport>();
			transport.WriteLineAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
			var          client          = new UciEngineClient(transport);
			var          ct              = CancellationToken.None;
			const string valueWithSpaces = @"C:\Chess\Table Bases\wdl345";

			// Act
			await client.SetOptionAsync("SyzygyPath", valueWithSpaces, ct);

			// Assert
			await transport.Received(1).WriteLineAsync($"setoption name SyzygyPath value {valueWithSpaces}", ct);
		}

		[Fact]
		public async Task SetOptionAsync_WhitespaceName_DoesNotSendCommand()
		{
			// Arrange
			var transport = Substitute.For<IUciTransport>();
			transport.WriteLineAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
			var client = new UciEngineClient(transport);
			var ct     = CancellationToken.None;

			// Act
			await client.SetOptionAsync("   ", "ignored", ct);

			// Assert
			await transport.DidNotReceiveWithAnyArgs().WriteLineAsync(default!);
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
			// Arrange
			var transport = Substitute.For<IUciTransport>();
			transport.WriteLineAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
			var client = new UciEngineClient(transport);
			var ct     = CancellationToken.None;

			// Act & Assert - invalid fen -> throws
			await FluentActions
				  .Awaiting(() => client.SetPositionAsync(Fen.Empty(), null, ct))
				  .Should()
				  .ThrowAsync<ArgumentException>("empty FEN should be rejected");

			// Act & Assert - valid fen without moves
			var fen = Fen.Default;
			await client.SetPositionAsync(fen, null, ct);
			await transport.Received().WriteLineAsync(Arg.Is<string>(s => s.StartsWith($"position fen {fen.Raw}")), ct);

			// Act & Assert - valid fen with moves
			await client.SetPositionAsync(fen, new[] { "e2e4", "e7e5" }, ct);
			await transport.Received().WriteLineAsync(Arg.Is<string>(s => s.Contains("moves e2e4 e7e5")), ct);
		}

		[Fact]
		public async Task IsReadyAsync_RespectsCancellationToken()
		{
			// Arrange
			var transport = Substitute.For<IUciTransport>();
			transport.WriteLineAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
			var       client = new UciEngineClient(transport);
			using var cts    = new CancellationTokenSource(TestConstants.CancellationTimeout);

			// Act + Assert
			await FluentActions
				  .Awaiting(() => client.IsReadyAsync(cts.Token))
				  .Should()
				  .ThrowAsync<OperationCanceledException>("operation should be cancelled");
		}

		[Fact]
		public async Task UciInitAsync_RespectsCancellationToken()
		{
			// Arrange
			var transport = Substitute.For<IUciTransport>();
			transport.WriteLineAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
			var       client = new UciEngineClient(transport);
			using var cts    = new CancellationTokenSource(TestConstants.CancellationTimeout);

			// Act + Assert
			await FluentActions
				  .Awaiting(() => client.UciInitAsync(cts.Token))
				  .Should()
				  .ThrowAsync<OperationCanceledException>("operation should be cancelled");
		}
	}

	public class Integration
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
				for (var i = 0; i < TestConstants.ExtendedHandshakePumpIterations && !pumpCts.IsCancellationRequested; i++)
				{
					await ch.Writer.WriteAsync("uciok");
					await Task.Delay(TestConstants.VeryShortDelay);
					await ch.Writer.WriteAsync("readyok");
					await Task.Delay(TestConstants.VeryShortDelay);
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
		fenResult.Should().NotBeNull("FEN result should be returned");
		fenResult!.Value.Raw.Should().Be(fen, "FEN raw should match");
		fenResult!.Value.Checkers.Should().Be("e1", "checkers should be e1");
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
				for (var i = 0; i < TestConstants.ExtendedHandshakePumpIterations && !pumpCts.IsCancellationRequested; i++)
				{
					await ch.Writer.WriteAsync("uciok");
					await Task.Delay(TestConstants.VeryShortDelay);
					await ch.Writer.WriteAsync("readyok");
					await Task.Delay(TestConstants.VeryShortDelay);
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
		await Task.Delay(TestConstants.MediumDelay);
		readyGate.TrySetResult();
		var moves = await perftTask;

		moves.Should().Contain("e2e4", "e2e4 should be parsed as a valid move");
		moves.Should().Contain("e7e5", "e7e5 should be parsed as a valid move");
		moves.Should().Contain("a7a8q", "a7a8q should be parsed as a valid move");
		moves.Should().Contain("h7h8N", "h7h8N should be parsed as a valid move");
		moves.Should().NotContain("bad", "invalid move notation should be filtered out");
		await transport.Received().WriteLineAsync("go perft 1", Arg.Any<CancellationToken>());
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
				for (var i = 0; i < TestConstants.HandshakePumpIterations && !pumpCts.IsCancellationRequested; i++)
				{
					await ch.Writer.WriteAsync("uciok");
					await Task.Delay(TestConstants.ShortDelay);
					await ch.Writer.WriteAsync("readyok");
					await Task.Delay(TestConstants.ShortDelay);
				}
			},
			pumpCts.Token);

		await client.StartAsync(CancellationToken.None);
		pumpCts.Cancel();
		await pump;

		// Validations
		await transport.Received().WriteLineAsync("uci",     Arg.Any<CancellationToken>());
		await transport.Received().WriteLineAsync("isready", Arg.Any<CancellationToken>());
		client.Activity.Should().Be(EngineActivity.Idle, "client should be idle after initialization");

		await client.DisposeAsync();
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
}
