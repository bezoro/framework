using FluentAssertions;
using JetBrains.Annotations;
using NSubstitute;

namespace Bezoro.Chess.UCI.Protocol.Tests.Domain;

[TestSubject(typeof(UciEngineClient))]
public class UciEngineClientGoFireAndForgetTests
{
	[Fact]
	public async Task GoFireAndForgetAsync_WhenCalledWithoutExplicitLimit_ShouldThrowArgumentException()
	{
		// Arrange
		var (transport, client) = UciEngineClientTestHelpers.CreateClientWithTransport();

		// Act
		var act = () => client.GoFireAndForgetAsync(new(), CancellationToken.None);

		// Assert
		await FluentActions.Awaiting(act)
						   .Should()
						   .ThrowAsync<ArgumentException>();
		await transport.DidNotReceiveWithAnyArgs().WriteLineAsync(default!, default);
	}

	[Fact]
	public async Task GoFireAndForgetAsync_WhenPonderIsEnabled_ShouldSetActivityToPondering()
	{
		// Arrange
		var (transport, client) = UciEngineClientTestHelpers.CreateClientWithTransport();

		// Act
		await client.GoFireAndForgetAsync(new() { Ponder = true }, CancellationToken.None);

		// Assert
		await transport.Received().WriteLineAsync("go ponder", CancellationToken.None);
		client.Activity.Should().Be(
			EngineActivity.Pondering,
			"activity should be Pondering when ponder is enabled"
		);
	}
}
