using FluentAssertions;
using JetBrains.Annotations;
using NSubstitute;

namespace Bezoro.Chess.UCI.Protocol.Tests.Domain;

[TestSubject(typeof(UciEngineClient))]
public class UciEngineClientStandardCommandTests
{
	[Fact]
	public async Task PonderHitAsync_WhenCalled_ShouldSendPonderHitCommand()
	{
		var (transport, client) = UciEngineClientTestHelpers.CreateClientWithTransport();

		await client.PonderHitAsync(CancellationToken.None);

		await transport.Received(1).WriteLineAsync("ponderhit", CancellationToken.None);
	}

	[Fact]
	public async Task RegisterAsync_WhenLaterIsRequested_ShouldSendRegisterLater()
	{
		var (transport, client) = UciEngineClientTestHelpers.CreateClientWithTransport();

		await client.RegisterAsync(UciRegistration.LaterOnly(), CancellationToken.None);

		await transport.Received(1).WriteLineAsync("register later", CancellationToken.None);
	}

	[Fact]
	public async Task RegisterAsync_WhenNameAndCodeAreProvided_ShouldSendRegisterCommand()
	{
		var (transport, client) = UciEngineClientTestHelpers.CreateClientWithTransport();

		await client.RegisterAsync(
			UciRegistration.WithCredentials("Bezoro", "abc-123"),
			CancellationToken.None
		);

		await transport.Received(1).WriteLineAsync(
			"register name Bezoro code abc-123",
			CancellationToken.None
		);
	}

	[Fact]
	public async Task RegisterAsync_WhenNameIsMissingForNonLaterRegistration_ShouldThrowArgumentException()
	{
		var (_, client) = UciEngineClientTestHelpers.CreateClientWithTransport();

		var act = () => client.RegisterAsync(new(false, null, "abc-123"), CancellationToken.None);

		await FluentActions.Awaiting(act)
						   .Should()
						   .ThrowAsync<ArgumentException>();
	}

	[Theory]
	[InlineData(true,  "debug on")]
	[InlineData(false, "debug off")]
	public async Task SetDebugAsync_WhenCalled_ShouldSendExpectedCommand(bool enabled, string expected)
	{
		var (transport, client) = UciEngineClientTestHelpers.CreateClientWithTransport();

		await client.SetDebugAsync(enabled, CancellationToken.None);

		await transport.Received(1).WriteLineAsync(expected, CancellationToken.None);
	}
}
