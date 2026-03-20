using FluentAssertions;
using JetBrains.Annotations;
using NSubstitute;

namespace Bezoro.Chess.UCI.Protocol.Tests.Domain;

[TestSubject(typeof(UciEngineClient))]
public class UciEngineClientGoFireAndForgetTests
{
	[Fact]
	public async Task GoFireAndForgetAsync_WhenCalled_ShouldWriteCommandAndSetActivityToSearching()
	{
		// Arrange
		var (transport, client) = UciEngineClientTestHelpers.CreateClientWithTransport();

		// Act
		await client.GoFireAndForgetAsync(new(), CancellationToken.None);

		// Assert
		await transport.Received().WriteLineAsync("go depth 6", CancellationToken.None);
		client.Activity.Should().Be(EngineActivity.Searching, "activity should be Searching after go command");
	}

	[Fact]
	public async Task GoFireAndForgetAsync_WhenPonderIsEnabled_ShouldSetActivityToPondering()
	{
		// Arrange
		var (transport, client) = UciEngineClientTestHelpers.CreateClientWithTransport();

		// Act
		await client.GoFireAndForgetAsync(new() { Ponder = true }, CancellationToken.None);

		// Assert
		await transport.Received().WriteLineAsync("go ponder depth 6", CancellationToken.None);
		client.Activity.Should().Be(
			EngineActivity.Pondering,
			"activity should be Pondering when ponder is enabled"
		);
	}
}
