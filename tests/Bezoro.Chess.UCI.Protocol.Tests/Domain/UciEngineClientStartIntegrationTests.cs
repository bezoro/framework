using FluentAssertions;
using JetBrains.Annotations;
using NSubstitute;

namespace Bezoro.Chess.UCI.Protocol.Tests.Domain;

[TestSubject(typeof(UciEngineClient))]
[Trait("Category", "Integration")]
public class UciEngineClientStartIntegrationTests
{
	[Fact(Timeout = 4000)]
	public async Task StartAsync_WhenCalled_ShouldSetActivityToIdle()
	{
		// Arrange
		var (transport, channel) = UciEngineClientTestHelpers.CreateMockTransport();

		// Act
		var client = await UciEngineClientTestHelpers.StartClientWithConcurrentHandshakeAsync(transport, channel);

		// Assert
		client.Activity.Should().Be(EngineActivity.Idle, "client should be idle after initialization");
	}

	[Fact(Timeout = 4000)]
	public async Task StartAsync_WhenCalled_ShouldWriteIsReadyCommand()
	{
		// Arrange
		var (transport, channel) = UciEngineClientTestHelpers.CreateMockTransport();

		// Act
		await UciEngineClientTestHelpers.StartClientWithConcurrentHandshakeAsync(transport, channel);

		// Assert
		await transport.Received().WriteLineAsync("isready", Arg.Any<CancellationToken>());
	}

	[Fact(Timeout = 4000)]
	public async Task StartAsync_WhenCalled_ShouldWriteUciCommand()
	{
		// Arrange
		var (transport, channel) = UciEngineClientTestHelpers.CreateMockTransport();

		// Act
		await UciEngineClientTestHelpers.StartClientWithConcurrentHandshakeAsync(transport, channel);

		// Assert
		await transport.Received().WriteLineAsync("uci", Arg.Any<CancellationToken>());
	}
}
