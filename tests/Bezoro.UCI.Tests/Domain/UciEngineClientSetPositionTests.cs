using Bezoro.UCI.API.Types;
using Bezoro.UCI.Domain;
using FluentAssertions;
using JetBrains.Annotations;
using NSubstitute;

namespace Bezoro.UCI.Tests.Domain;

[TestSubject(typeof(UciEngineClient))]
public class UciEngineClientSetPositionTests
{
	[Fact]
	public async Task WhenInvalidFen_ShouldThrow()
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
	public async Task WithValidFenWithMoves_ShouldSendCommandWithMoves()
	{
		// Arrange
		var (transport, client) = UciEngineClientTestHelpers.CreateClientWithTransport();
		var ct  = CancellationToken.None;
		var fen = Fen.Default;

		// Act
		await client.SetPositionAsync(fen, ["e2e4", "e7e5"], ct);

		// Assert
		await transport.Received().WriteLineAsync(Arg.Is<string>(s => s.Contains("moves e2e4 e7e5")), ct);
	}

	[Fact]
	public async Task WithValidFenWithoutMoves_ShouldSendCommand()
	{
		// Arrange
		var (transport, client) = UciEngineClientTestHelpers.CreateClientWithTransport();
		var ct  = CancellationToken.None;
		var fen = Fen.Default;

		// Act
		await client.SetPositionAsync(fen, null, ct);

		// Assert
		await transport.Received().WriteLineAsync(
			Arg.Is<string>(s => s.StartsWith($"position fen {fen.Raw}")),
			ct
		);
	}
}
