using Bezoro.UCI.Domain;
using JetBrains.Annotations;
using NSubstitute;

namespace Bezoro.UCI.Tests.Domain;

[TestSubject(typeof(UciEngineClient))]
public class UciEngineClientSetOptionTests
{
	[Fact]
	public async Task SetOptionAsync_WhenNameIsWhitespace_ShouldNotSendCommand()
	{
		// Arrange
		var (transport, client) = UciEngineClientTestHelpers.CreateClientWithTransport();
		var ct = CancellationToken.None;

		// Act
		await client.SetOptionAsync("   ", "ignored", ct);

		// Assert
		await transport.DidNotReceiveWithAnyArgs().WriteLineAsync(default!);
	}

	[Fact]
	public async Task SetOptionAsync_WhenValueContainsSpaces_ShouldSendVerbatimValue()
	{
		// Arrange
		var (transport, client) = UciEngineClientTestHelpers.CreateClientWithTransport();
		var          ct                = CancellationToken.None;
		const string VALUE_WITH_SPACES = @"C:\Chess\Table Bases\wdl345";

		// Act
		await client.SetOptionAsync("SyzygyPath", VALUE_WITH_SPACES, ct);

		// Assert
		await transport.Received(1).WriteLineAsync($"setoption name SyzygyPath value {VALUE_WITH_SPACES}", ct);
	}

	[Fact]
	public async Task SetOptionAsync_WhenValueIsNull_ShouldSendNameOnlyCommand()
	{
		// Arrange
		var (transport, client) = UciEngineClientTestHelpers.CreateClientWithTransport();
		var ct = new CancellationTokenSource().Token;

		// Act
		await client.SetOptionAsync("Ponder", null, ct);

		// Assert
		await transport.Received(1).WriteLineAsync("setoption name Ponder", ct);
	}

	[Fact]
	public async Task SetOptionAsync_WhenValueIsProvided_ShouldSendNameAndValueCommand()
	{
		// Arrange
		var (transport, client) = UciEngineClientTestHelpers.CreateClientWithTransport();
		var ct = new CancellationTokenSource().Token;

		// Act
		await client.SetOptionAsync("Hash", "256", ct);

		// Assert
		await transport.Received(1).WriteLineAsync("setoption name Hash value 256", ct);
	}
}
