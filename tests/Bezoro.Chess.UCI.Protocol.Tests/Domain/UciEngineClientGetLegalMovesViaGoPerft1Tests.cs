using Bezoro.Chess.UCI.Protocol.Tests.TestHelpers;
using FluentAssertions;
using JetBrains.Annotations;
using NSubstitute;

namespace Bezoro.Chess.UCI.Protocol.Tests.Domain;

[TestSubject(typeof(UciEngineClient))]
public class UciEngineClientGetLegalMovesViaGoPerft1Tests
{
	[Fact]
	public async Task GetLegalMovesViaPerftAsync_WhenCalled_ShouldFilterInvalidMoves()
	{
		// Arrange
		var (transport, channel) = UciEngineClientTestHelpers.CreateMockTransport();
		var client        = await UciEngineClientTestHelpers.StartClientWithHandshakeAsync(transport, channel);
		var completeReady = UciEngineClientTestHelpers.SetupDelayedReadyResponse(transport, channel);

		// Act
		var perftTask = client.GetLegalMovesViaPerftAsync(CancellationToken.None);
		await channel.Writer.WriteAsync("e2e4 ; bad");
		await Task.Delay(TestConstants.MediumDelay);
		completeReady();
		var moves = await perftTask;

		// Assert
		moves.Should().Contain("e2e4", "valid move should be included");
		moves.Should().NotContain("bad", "invalid move notation should be filtered out");
	}

	[Fact]
	public async Task GetLegalMovesViaPerftAsync_WhenCalled_ShouldHandleMixedValidAndInvalidMoves()
	{
		// Arrange
		var (transport, channel) = UciEngineClientTestHelpers.CreateMockTransport();
		var client        = await UciEngineClientTestHelpers.StartClientWithHandshakeAsync(transport, channel);
		var completeReady = UciEngineClientTestHelpers.SetupDelayedReadyResponse(transport, channel);

		// Act
		var perftTask = client.GetLegalMovesViaPerftAsync(CancellationToken.None);
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

	[Fact]
	public async Task GetLegalMovesViaPerftAsync_WhenCalled_ShouldParseMultipleValidMoves()
	{
		// Arrange
		var (transport, channel) = UciEngineClientTestHelpers.CreateMockTransport();
		var client        = await UciEngineClientTestHelpers.StartClientWithHandshakeAsync(transport, channel);
		var completeReady = UciEngineClientTestHelpers.SetupDelayedReadyResponse(transport, channel);

		// Act
		var perftTask = client.GetLegalMovesViaPerftAsync(CancellationToken.None);
		await channel.Writer.WriteAsync("e2e4, e7e5");
		await Task.Delay(TestConstants.MediumDelay);
		completeReady();
		var moves = await perftTask;

		// Assert
		moves.Should().Contain("e2e4", "e2e4 should be parsed");
		moves.Should().Contain("e7e5", "e7e5 should be parsed");
	}

	[Fact]
	public async Task GetLegalMovesViaPerftAsync_WhenCalled_ShouldParsePromotionMoves()
	{
		// Arrange
		var (transport, channel) = UciEngineClientTestHelpers.CreateMockTransport();
		var client        = await UciEngineClientTestHelpers.StartClientWithHandshakeAsync(transport, channel);
		var completeReady = UciEngineClientTestHelpers.SetupDelayedReadyResponse(transport, channel);

		// Act
		var perftTask = client.GetLegalMovesViaPerftAsync(CancellationToken.None);
		await channel.Writer.WriteAsync("a7a8q : h7h8N");
		await Task.Delay(TestConstants.MediumDelay);
		completeReady();
		var moves = await perftTask;

		// Assert
		moves.Should().Contain("a7a8q", "a7a8q should be parsed as a valid promotion move");
		moves.Should().Contain("h7h8N", "h7h8N should be parsed as a valid promotion move");
	}

	[Fact]
	public async Task GetLegalMovesViaPerftAsync_WhenCalled_ShouldParseValidMove()
	{
		// Arrange
		var (transport, channel) = UciEngineClientTestHelpers.CreateMockTransport();
		var client        = await UciEngineClientTestHelpers.StartClientWithHandshakeAsync(transport, channel);
		var completeReady = UciEngineClientTestHelpers.SetupDelayedReadyResponse(transport, channel);

		// Act
		var perftTask = client.GetLegalMovesViaPerftAsync(CancellationToken.None);
		await channel.Writer.WriteAsync("e2e4");
		await Task.Delay(TestConstants.MediumDelay);
		completeReady();
		var moves = await perftTask;

		// Assert
		moves.Should().Contain("e2e4", "e2e4 should be parsed as a valid move");
	}

	[Fact]
	public async Task GetLegalMovesViaPerftAsync_WhenCalled_ShouldSendGoPerftCommand()
	{
		// Arrange
		var (transport, channel) = UciEngineClientTestHelpers.CreateMockTransport();
		var client        = await UciEngineClientTestHelpers.StartClientWithHandshakeAsync(transport, channel);
		var completeReady = UciEngineClientTestHelpers.SetupDelayedReadyResponse(transport, channel);

		// Act
		var perftTask = client.GetLegalMovesViaPerftAsync(CancellationToken.None);
		await channel.Writer.WriteAsync("e2e4");
		await Task.Delay(TestConstants.MediumDelay);
		completeReady();
		await perftTask;

		// Assert
		await transport.Received().WriteLineAsync("go perft 1", Arg.Any<CancellationToken>());
	}
}
