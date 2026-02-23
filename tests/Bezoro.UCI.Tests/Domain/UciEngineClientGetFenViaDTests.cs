using Bezoro.UCI.API.Types;
using Bezoro.UCI.Domain;
using FluentAssertions;
using JetBrains.Annotations;
using NSubstitute;

namespace Bezoro.UCI.Tests.Domain;

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
}
