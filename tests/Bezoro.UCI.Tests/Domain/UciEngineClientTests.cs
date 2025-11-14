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
public static class UciEngineClientTests
{
	public static class IntegrationTests
	{
		public class GetFenViaDTests
		{
			[Fact(Timeout = 4000)]
			public async Task GetFenViaDAsync_ShouldReturnCheckersWhenPresent()
			{
				// Arrange
				var (transport, channel) = CreateMockTransport();
				var client = await StartClientWithHandshakeAsync(transport, channel);

				string fen = Fen.Default.Raw;
				transport.ClearReceivedCalls();
				transport.When(x => x.WriteLineAsync("d", Arg.Any<CancellationToken>()))
						 .Do(async _ =>
						 {
							 await channel.Writer.WriteAsync($"fen: {fen}");
							 await channel.Writer.WriteAsync("checkers: e1");
						 });

				// Act
				var fenResult = await client.GetFenViaDAsync(CancellationToken.None);

				// Assert
				fenResult.Should().NotBeNull("FEN result should be returned");
				fenResult!.Value.Checkers.Should().Be("e1", "checkers should be e1");
			}

			[Fact(Timeout = 4000)]
			public async Task GetFenViaDAsync_ShouldReturnFen()
			{
				// Arrange
				var (transport, channel) = CreateMockTransport();
				var client = await StartClientWithHandshakeAsync(transport, channel);

				string fen = Fen.Default.Raw;
				transport.ClearReceivedCalls();
				transport.When(x => x.WriteLineAsync("d", Arg.Any<CancellationToken>()))
						 .Do(async _ => { await channel.Writer.WriteAsync($"fen: {fen}"); });

				// Act
				var fenResult = await client.GetFenViaDAsync(CancellationToken.None);

				// Assert
				fenResult.Should().NotBeNull("FEN result should be returned");
				fenResult!.Value.Raw.Should().Be(fen, "FEN raw should match");
			}
		}

		public class GetLegalMovesViaGoPerft1Tests
		{
			[Fact(Timeout = 3000)]
			public async Task GetLegalMovesViaGoPerft1Async_ShouldFilterInvalidMoves()
			{
				// Arrange
				var (transport, channel) = CreateMockTransport();
				var client        = await StartClientWithHandshakeAsync(transport, channel);
				var completeReady = SetupDelayedReadyResponse(transport, channel);

				// Act
				var perftTask = client.GetLegalMovesViaGoPerft1Async(CancellationToken.None);
				await channel.Writer.WriteAsync("e2e4 ; bad");
				await Task.Delay(TestConstants.MediumDelay);
				completeReady();
				var moves = await perftTask;

				// Assert
				moves.Should().Contain("e2e4", "valid move should be included");
				moves.Should().NotContain("bad", "invalid move notation should be filtered out");
			}

			[Fact(Timeout = 3000)]
			public async Task GetLegalMovesViaGoPerft1Async_ShouldHandleMixedValidAndInvalidMoves()
			{
				// Arrange
				var (transport, channel) = CreateMockTransport();
				var client        = await StartClientWithHandshakeAsync(transport, channel);
				var completeReady = SetupDelayedReadyResponse(transport, channel);

				// Act
				var perftTask = client.GetLegalMovesViaGoPerft1Async(CancellationToken.None);
				await channel.Writer.WriteAsync("e2e4, e7e5 ; bad | a7a8q : h7h8N");
				await Task.Delay(TestConstants.MediumDelay);
				completeReady();
				var moves = await perftTask;

				// Assert
				moves.Should().Contain("e2e4",  "e2e4 should be parsed");
				moves.Should().Contain("e7e5",  "e7e5 should be parsed");
				moves.Should().Contain("a7a8q", "a7a8q should be parsed");
				moves.Should().Contain("h7h8N", "h7h8N should be parsed");
				moves.Should().NotContain("bad", "invalid move should be filtered");
			}

			[Fact(Timeout = 3000)]
			public async Task GetLegalMovesViaGoPerft1Async_ShouldParseMultipleValidMoves()
			{
				// Arrange
				var (transport, channel) = CreateMockTransport();
				var client        = await StartClientWithHandshakeAsync(transport, channel);
				var completeReady = SetupDelayedReadyResponse(transport, channel);

				// Act
				var perftTask = client.GetLegalMovesViaGoPerft1Async(CancellationToken.None);
				await channel.Writer.WriteAsync("e2e4, e7e5");
				await Task.Delay(TestConstants.MediumDelay);
				completeReady();
				var moves = await perftTask;

				// Assert
				moves.Should().Contain("e2e4", "e2e4 should be parsed");
				moves.Should().Contain("e7e5", "e7e5 should be parsed");
			}

			[Fact(Timeout = 3000)]
			public async Task GetLegalMovesViaGoPerft1Async_ShouldParsePromotionMoves()
			{
				// Arrange
				var (transport, channel) = CreateMockTransport();
				var client        = await StartClientWithHandshakeAsync(transport, channel);
				var completeReady = SetupDelayedReadyResponse(transport, channel);

				// Act
				var perftTask = client.GetLegalMovesViaGoPerft1Async(CancellationToken.None);
				await channel.Writer.WriteAsync("a7a8q : h7h8N");
				await Task.Delay(TestConstants.MediumDelay);
				completeReady();
				var moves = await perftTask;

				// Assert
				moves.Should().Contain("a7a8q", "a7a8q should be parsed as a valid promotion move");
				moves.Should().Contain("h7h8N", "h7h8N should be parsed as a valid promotion move");
			}

			[Fact(Timeout = 3000)]
			public async Task GetLegalMovesViaGoPerft1Async_ShouldParseValidMove()
			{
				// Arrange
				var (transport, channel) = CreateMockTransport();
				var client        = await StartClientWithHandshakeAsync(transport, channel);
				var completeReady = SetupDelayedReadyResponse(transport, channel);

				// Act
				var perftTask = client.GetLegalMovesViaGoPerft1Async(CancellationToken.None);
				await channel.Writer.WriteAsync("e2e4");
				await Task.Delay(TestConstants.MediumDelay);
				completeReady();
				var moves = await perftTask;

				// Assert
				moves.Should().Contain("e2e4", "e2e4 should be parsed as a valid move");
			}

			[Fact(Timeout = 3000)]
			public async Task GetLegalMovesViaGoPerft1Async_ShouldSendGoPerftCommand()
			{
				// Arrange
				var (transport, channel) = CreateMockTransport();
				var client        = await StartClientWithHandshakeAsync(transport, channel);
				var completeReady = SetupDelayedReadyResponse(transport, channel);

				// Act
				var perftTask = client.GetLegalMovesViaGoPerft1Async(CancellationToken.None);
				await channel.Writer.WriteAsync("e2e4");
				await Task.Delay(TestConstants.MediumDelay);
				completeReady();
				await perftTask;

				// Assert
				await transport.Received().WriteLineAsync("go perft 1", Arg.Any<CancellationToken>());
			}

			private static Action SetupDelayedReadyResponse(IUciTransport transport, Channel<string> channel)
			{
				var readyGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
				transport.ClearReceivedCalls();
				transport.When(x => x.WriteLineAsync("isready", Arg.Any<CancellationToken>()))
						 .Do(async _ =>
						 {
							 await readyGate.Task;
							 await channel.Writer.WriteAsync("readyok");
						 });

				return () => readyGate.TrySetResult();
			}
		}

		public class StartTests
		{
			[Fact(Timeout = 4000)]
			public async Task StartAsync_ShouldSetActivityToIdle()
			{
				// Arrange
				var (transport, channel) = CreateMockTransport();

				// Act
				var client = await StartClientWithConcurrentHandshakeAsync(transport, channel);

				// Assert
				client.Activity.Should().Be(EngineActivity.Idle, "client should be idle after initialization");
			}

			[Fact(Timeout = 4000)]
			public async Task StartAsync_ShouldWriteIsReadyCommand()
			{
				// Arrange
				var (transport, channel) = CreateMockTransport();

				// Act
				var client = await StartClientWithConcurrentHandshakeAsync(transport, channel);

				// Assert
				await transport.Received().WriteLineAsync("isready", Arg.Any<CancellationToken>());
			}

			[Fact(Timeout = 4000)]
			public async Task StartAsync_ShouldWriteUciCommand()
			{
				// Arrange
				var (transport, channel) = CreateMockTransport();

				// Act
				var client = await StartClientWithConcurrentHandshakeAsync(transport, channel);

				// Assert
				await transport.Received().WriteLineAsync("uci", Arg.Any<CancellationToken>());
			}
		}

		#region Helper Methods

		private static (IUciTransport transport, Channel<string> channel) CreateMockTransport()
		{
			var transport = Substitute.For<IUciTransport>();
			transport.StartAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
			transport.StopAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
			transport.WriteLineAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);

			var channel = Channel.CreateUnbounded<string>();
			transport.ReadLinesAsync(Arg.Any<CancellationToken>())
					 .Returns(ci => StreamFromChannel(channel.Reader, (CancellationToken)ci[0]));

			return (transport, channel);
		}

		private static async Task<UciEngineClient> StartClientWithHandshakeAsync(
			IUciTransport   transport,
			Channel<string> channel,
			int             pumpIterations = TestConstants.ExtendedHandshakePumpIterations)
		{
			var client = new UciEngineClient(transport);

			using var pumpCts = new CancellationTokenSource();
			var pump = Task.Run(
				async () =>
				{
					for (var i = 0; i < pumpIterations && !pumpCts.IsCancellationRequested; i++)
					{
						await channel.Writer.WriteAsync("uciok");
						await Task.Delay(TestConstants.VeryShortDelay);
						await channel.Writer.WriteAsync("readyok");
						await Task.Delay(TestConstants.VeryShortDelay);
					}
				},
				pumpCts.Token);

			await client.StartAsync(CancellationToken.None);
			pumpCts.Cancel();
			await pump;

			return client;
		}

		private static async Task<UciEngineClient> StartClientWithConcurrentHandshakeAsync(
			IUciTransport   transport,
			Channel<string> channel)
		{
			var client = new UciEngineClient(transport);

			using var pumpCts = new CancellationTokenSource();
			var pump = Task.Run(
				async () =>
				{
					for (var i = 0; i < TestConstants.HandshakePumpIterations && !pumpCts.IsCancellationRequested; i++)
					{
						await channel.Writer.WriteAsync("uciok");
						await Task.Delay(TestConstants.ShortDelay);
						await channel.Writer.WriteAsync("readyok");
						await Task.Delay(TestConstants.ShortDelay);
					}
				},
				pumpCts.Token);

			await client.StartAsync(CancellationToken.None);
			pumpCts.Cancel();
			await pump;

			return client;
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

		#endregion
	}

	public static class UnitTests
	{
		public class BuildGoCommandTests
		{
			[Fact]
			public void ShouldAddDefaultDepthWhenNoLimits()
			{
				string cmd = UciEngineClient.BuildGoCommand(new());
				cmd.Should().Be("go depth 6", "default depth should be 6");
			}

			[Fact]
			public void ShouldCombinePonderAndInfiniteFlags()
			{
				string cmd = UciEngineClient.BuildGoCommand(new() { Ponder = true, Infinite = true });
				cmd.Should().Be("go ponder infinite", "ponder and infinite flags should be combined");
			}

			[Fact]
			public void ShouldFilterAndLowercaseSearchmoves()
			{
				string cmd = UciEngineClient.BuildGoCommand(
					new() { SearchMoves = new[] { "E2E4", "bad", "a7a8Q", "" } });

				cmd.Should().Be("go depth 6 searchmoves e2e4 a7a8q", "searchmoves should be filtered and lowercased");
			}

			[Fact]
			public void ShouldFormatTimeControlsCorrectly()
			{
				string cmd = UciEngineClient.BuildGoCommand(
					new() { WhiteTimeMs = 1000, BlackTimeMs = 2000, WhiteIncrementMs = 10, BlackIncrementMs = 20 });

				cmd.Should().Be(
					"go wtime 1000 btime 2000 winc 10 binc 20",
					"time controls should be formatted correctly"
				);
			}

			[Fact]
			public void ShouldIncludeNodesDepthMate()
			{
				string cmd = UciEngineClient.BuildGoCommand(new() { Nodes = 123, Depth = 7, Mate = 2 });
				cmd.Should().Be("go nodes 123 depth 7 mate 2", "nodes, depth, and mate should be included");
			}
		}

		public class GoFireAndForgetTests
		{
			[Fact]
			public async Task ShouldWriteCommandAndSetActivityToSearching()
			{
				// Arrange
				var (transport, client) = CreateClientWithTransport();

				// Act
				await client.GoFireAndForgetAsync(new(), CancellationToken.None);

				// Assert
				await transport.Received().WriteLineAsync("go depth 6", CancellationToken.None);
				client.Activity.Should().Be(EngineActivity.Searching, "activity should be Searching after go command");
			}

			[Fact]
			public async Task WithPonder_ShouldSetActivityToPondering()
			{
				// Arrange
				var (transport, client) = CreateClientWithTransport();

				// Act
				await client.GoFireAndForgetAsync(new() { Ponder = true }, CancellationToken.None);

				// Assert
				await transport.Received().WriteLineAsync("go ponder depth 6", CancellationToken.None);
				client.Activity.Should().Be(
					EngineActivity.Pondering,
					"activity should be Pondering when ponder is enabled");
			}

			private static (IUciTransport transport, UciEngineClient client) CreateClientWithTransport()
			{
				var transport = Substitute.For<IUciTransport>();
				transport.WriteLineAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
				var client = new UciEngineClient(transport);
				return (transport, client);
			}
		}

		public class IsReadyTests
		{
			[Fact]
			public async Task WhenCancelled_ShouldThrowOperationCanceledException()
			{
				// Arrange
				var (_, client) = CreateClientWithTransport();
				var cts = new CancellationTokenSource(TestConstants.CancellationTimeout);

				// Act + Assert
				await FluentActions
					  .Awaiting(() => client.IsReadyAsync(cts.Token))
					  .Should()
					  .ThrowAsync<OperationCanceledException>("operation should be cancelled");
			}

			private static (IUciTransport transport, UciEngineClient client) CreateClientWithTransport()
			{
				var transport = Substitute.For<IUciTransport>();
				transport.WriteLineAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
				var client = new UciEngineClient(transport);
				return (transport, client);
			}
		}

		public class IsUciMoveStringTests
		{
			[Theory]
			[InlineData("e9e4",  false, "e9 is an invalid square")]
			[InlineData("i2e4",  false, "i2 is an invalid square")]
			[InlineData("e2e",   false, "incomplete move notation")]
			[InlineData("e2e45", false, "rank out of bounds")]
			[InlineData("e2e4x", false, "extra characters")]
			[InlineData("e2e4",  true,  "e2e4 is a valid UCI move")]
			[InlineData("a7a8q", true,  "a7a8q is a valid promotion move")]
			[InlineData("H7H8N", true,  "H7H8N is a valid move (case insensitive)")]
			public void CoveringVariousCases(string move, bool expected, string message)
			{
				UciEngineClient.IsUciMoveString(move).Should().Be(expected, message);
			}
		}

		public class SetOptionTests
		{
			[Fact]
			public async Task WhenNameIsWhitespace_ShouldNotSendCommand()
			{
				// Arrange
				var (transport, client) = CreateClientWithTransport();
				var ct = CancellationToken.None;

				// Act
				await client.SetOptionAsync("   ", "ignored", ct);

				// Assert
				await transport.DidNotReceiveWithAnyArgs().WriteLineAsync(default!);
			}

			[Fact]
			public async Task WhenNoValue_ShouldSendCorrectUciCommand()
			{
				// Arrange
				var (transport, client) = CreateClientWithTransport();
				var ct = new CancellationTokenSource().Token;

				// Act
				await client.SetOptionAsync("Ponder", null, ct);

				// Assert
				await transport.Received(1).WriteLineAsync("setoption name Ponder", ct);
			}

			[Fact]
			public async Task WhenValueContainsSpaces_ShouldSendVerbatimValue()
			{
				// Arrange
				var (transport, client) = CreateClientWithTransport();
				var          ct              = CancellationToken.None;
				const string valueWithSpaces = @"C:\Chess\Table Bases\wdl345";

				// Act
				await client.SetOptionAsync("SyzygyPath", valueWithSpaces, ct);

				// Assert
				await transport.Received(1).WriteLineAsync($"setoption name SyzygyPath value {valueWithSpaces}", ct);
			}

			[Fact]
			public async Task WhenValueProvided_ShouldSendCorrectUciCommand()
			{
				// Arrange
				var (transport, client) = CreateClientWithTransport();
				var ct = new CancellationTokenSource().Token;

				// Act
				await client.SetOptionAsync("Hash", "256", ct);

				// Assert
				await transport.Received(1).WriteLineAsync("setoption name Hash value 256", ct);
			}

			private static (IUciTransport transport, UciEngineClient client) CreateClientWithTransport()
			{
				var transport = Substitute.For<IUciTransport>();
				transport.WriteLineAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
				var client = new UciEngineClient(transport);
				return (transport, client);
			}
		}

		public class SetPositionTests
		{
			[Fact]
			public async Task WhenInvalidFen_ShouldThrow()
			{
				// Arrange
				var (_, client) = CreateClientWithTransport();
				var ct = CancellationToken.None;

				// Act & Assert - invalid fen -> throws
				await FluentActions
					  .Awaiting(() => client.SetPositionAsync(Fen.Empty(), null, ct))
					  .Should()
					  .ThrowAsync<ArgumentException>("empty FEN should be rejected");
			}

			[Fact]
			public async Task WithValidFenWithMoves_ShouldSendCommandWithMoves()
			{
				// Arrange
				var (transport, client) = CreateClientWithTransport();
				var ct  = CancellationToken.None;
				var fen = Fen.Default;

				// Act
				await client.SetPositionAsync(fen, new[] { "e2e4", "e7e5" }, ct);

				// Assert
				await transport.Received().WriteLineAsync(Arg.Is<string>(s => s.Contains("moves e2e4 e7e5")), ct);
			}

			[Fact]
			public async Task WithValidFenWithoutMoves_ShouldSendCommand()
			{
				// Arrange
				var (transport, client) = CreateClientWithTransport();
				var ct  = CancellationToken.None;
				var fen = Fen.Default;

				// Act
				await client.SetPositionAsync(fen, null, ct);

				// Assert
				await transport.Received().WriteLineAsync(
					Arg.Is<string>(s => s.StartsWith($"position fen {fen.Raw}")),
					ct);
			}

			private static (IUciTransport transport, UciEngineClient client) CreateClientWithTransport()
			{
				var transport = Substitute.For<IUciTransport>();
				transport.WriteLineAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
				var client = new UciEngineClient(transport);
				return (transport, client);
			}
		}

		public class UciInitTests
		{
			[Fact]
			public async Task WhenCancelled_ShouldThrowOperationCanceledException()
			{
				// Arrange
				var (_, client) = CreateClientWithTransport();
				var cts = new CancellationTokenSource(TestConstants.CancellationTimeout);

				// Act + Assert
				await FluentActions
					  .Awaiting(() => client.UciInitAsync(cts.Token))
					  .Should()
					  .ThrowAsync<OperationCanceledException>("operation should be cancelled");
			}

			private static (IUciTransport transport, UciEngineClient client) CreateClientWithTransport()
			{
				var transport = Substitute.For<IUciTransport>();
				transport.WriteLineAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
				var client = new UciEngineClient(transport);
				return (transport, client);
			}
		}
	}
}
