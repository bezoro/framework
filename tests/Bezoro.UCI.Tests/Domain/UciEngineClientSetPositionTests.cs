using Bezoro.UCI.API.Types;
using Bezoro.UCI.Domain;
using Bezoro.UCI.Tests.TestHelpers;
using FluentAssertions;
using JetBrains.Annotations;
using NSubstitute;

namespace Bezoro.UCI.Tests.Domain;

[TestSubject(typeof(UciEngineClient))]
public class UciEngineClientSetPositionTests
{
	[Fact]
	public async Task SetPositionAsync_WhenFenAndMovesAreProvided_ShouldSendPositionCommandWithMoves()
	{
		// Arrange
		var (transport, client) = UciEngineClientTestHelpers.CreateClientWithTransport();
		var ct  = CancellationToken.None;
		var fen = Fen.Default;

		// Act
		await client.SetPositionAsync(fen, ["e2e4", "e7e5"], ct);

		// Assert
		await transport.Received().WriteLineAsync("position startpos moves e2e4 e7e5", ct);
	}

	[Fact]
	public async Task SetPositionAsync_WhenFenIsInvalid_ShouldThrowArgumentException()
	{
		// Arrange
		var (_, client) = UciEngineClientTestHelpers.CreateClientWithTransport();
		var ct = CancellationToken.None;

		// Act & Assert - invalid fen -> throws
		await FluentActions
			  .Awaiting(() => client.SetPositionAsync(Fen.Empty(), null, ct))
			  .Should()
			  .ThrowAsync<ArgumentException>("empty FEN should be rejected");
	}

	[Fact]
	public async Task SetPositionAsync_WhenOnlyFenIsProvided_ShouldSendPositionCommandWithoutMoves()
	{
		// Arrange
		var (transport, client) = UciEngineClientTestHelpers.CreateClientWithTransport();
		var ct  = CancellationToken.None;
		var fen = Fen.Default;

		// Act
		await client.SetPositionAsync(fen, null, ct);

		// Assert
		await transport.Received().WriteLineAsync("position startpos", ct);
	}

	[Fact]
	public async Task SetPositionAsync_WhenFenIsNotStartPosition_ShouldSendFenCommand()
	{
		// Arrange
		var (transport, client) = UciEngineClientTestHelpers.CreateClientWithTransport();
		var ct  = CancellationToken.None;
		var fen = Fen.Parse(TestConstants.AFTER_E2_E4_FEN);

		fen.Should().NotBeNull();

		// Act
		await client.SetPositionAsync(fen!.Value, null, ct);

		// Assert
		await transport.Received().WriteLineAsync($"position fen {fen.Value.Raw}", ct);
	}
}
