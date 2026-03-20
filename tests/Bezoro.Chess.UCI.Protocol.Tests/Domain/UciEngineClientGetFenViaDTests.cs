using Bezoro.Chess.UCI.Protocol.API.Types;
using Bezoro.Chess.UCI.Protocol.API;
using Bezoro.Chess.UCI.Protocol.Tests.TestHelpers;
using FluentAssertions;
using JetBrains.Annotations;
using NSubstitute;

namespace Bezoro.Chess.UCI.Protocol.Tests.Domain;

[TestSubject(typeof(UciEngineClient))]
public class UciEngineClientGetFenViaDTests
{
	[Fact(Timeout = 4000)]
	public async Task GetFenViaDAsync_WhenCalled_ShouldReturnCheckersWhenPresent()
	{
		// Arrange
		var (transport, channel) = UciEngineClientTestHelpers.CreateMockTransport();
		var client = await UciEngineClientTestHelpers.StartClientWithHandshakeAsync(transport, channel);

		string fen = Fen.Default.Raw;
		transport.ClearReceivedCalls();
		transport.When(x => x.WriteLineAsync("d", Arg.Any<CancellationToken>()))
				 .Do(async _ =>
					 {
						 await channel.Writer.WriteAsync($"fen: {fen}");
						 await channel.Writer.WriteAsync("checkers: e1");
					 }
				 );

		// Act
		var fenResult = await client.GetFenViaDAsync(CancellationToken.None);

		// Assert
		fenResult.Should().NotBeNull("FEN result should be returned");
		fenResult!.Value.Checkers.Should().Be("e1", "checkers should be e1");
	}

	[Fact(Timeout = 4000)]
	public async Task GetFenViaDAsync_WhenCalled_ShouldReturnFen()
	{
		// Arrange
		var (transport, channel) = UciEngineClientTestHelpers.CreateMockTransport();
		var client = await UciEngineClientTestHelpers.StartClientWithHandshakeAsync(transport, channel);

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

	[Fact(Timeout = 4000)]
	public async Task GetFenViaDAsync_WhenCalledConcurrently_ShouldKeepResponsesBoundToEachRequest()
	{
		// Arrange
		var (transport, channel) = UciEngineClientTestHelpers.CreateMockTransport();
		var client = await UciEngineClientTestHelpers.StartClientWithHandshakeAsync(transport, channel);

		var releaseResponses = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		int requestCount = 0;

		string firstFen  = Fen.Default.Raw;
		string secondFen = "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq - 0 1";

		transport.ClearReceivedCalls();
		transport.When(x => x.WriteLineAsync("d", Arg.Any<CancellationToken>()))
				 .Do(
					 async _ =>
					 {
						 int invocation = Interlocked.Increment(ref requestCount);
						 await releaseResponses.Task;

						 string fenLine = invocation == 1 ? firstFen : secondFen;
						 await channel.Writer.WriteAsync($"fen: {fenLine}");
						 await channel.Writer.WriteAsync("checkers:");
					 }
				 );

		// Act
		Task<Fen?> firstRequest  = client.GetFenViaDAsync(CancellationToken.None);
		Task<Fen?> secondRequest = client.GetFenViaDAsync(CancellationToken.None);

		releaseResponses.TrySetResult();
		Fen?[] results = await Task.WhenAll(firstRequest, secondRequest).WaitAsync(TestConstants.DefaultTimeout);

		// Assert
		results[0].Should().NotBeNull();
		results[1].Should().NotBeNull();
		results[0]!.Value.Raw.Should().Be(firstFen);
		results[1]!.Value.Raw.Should().Be(secondFen);
		requestCount.Should().Be(2, "each request should issue its own display-board command");
	}
}
