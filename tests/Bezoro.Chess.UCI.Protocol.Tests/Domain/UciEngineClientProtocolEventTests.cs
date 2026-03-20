using Bezoro.Chess.UCI.Protocol.Tests.TestHelpers;
using FluentAssertions;
using JetBrains.Annotations;
using NSubstitute;

namespace Bezoro.Chess.UCI.Protocol.Tests.Domain;

[TestSubject(typeof(UciEngineClient))]
public class UciEngineClientProtocolEventTests
{
	[Fact]
	public async Task GoAsync_WhenInfoAndBestMoveLinesArrive_ShouldPublishTypedEvents()
	{
		var (transport, channel) = UciEngineClientTestHelpers.CreateMockTransport();
		var                 client = await UciEngineClientTestHelpers.StartClientWithHandshakeAsync(transport, channel);
		var                 infoMessages = new List<UciInfoMessage>();
		UciBestMoveMessage? bestMove = null;

		client.InfoReceived            += info => infoMessages.Add(info);
		client.BestMoveMessageReceived += message => bestMove = message;

		var goTask = client.GoAsync(new() { Depth = 4 }, CancellationToken.None);

		await channel.Writer.WriteAsync("info depth 4 seldepth 6 multipv 1 score cp 23 nodes 100 time 10 pv e2e4 e7e5");
		await channel.Writer.WriteAsync("bestmove e2e4 ponder e7e5");

		var result = await goTask.WaitAsync(TestConstants.DefaultTimeout);

		result.BestMove.Should().Be("e2e4");
		infoMessages.Should().ContainSingle();
		infoMessages[0].Payload.PrincipalVariation.Should().NotBeNull();
		bestMove.Should().NotBeNull();
		bestMove!.BestMove.Should().Be("e2e4");
		bestMove.PonderMove.Should().Be("e7e5");

		await client.DisposeAsync();
	}

	[Fact]
	public async Task StartAsync_WhenHandshakeLinesArrive_ShouldPublishTypedProtocolMessages()
	{
		var (transport, channel) = UciEngineClientTestHelpers.CreateMockTransport();
		var client   = new UciEngineClient(transport);
		var messages = new List<UciProtocolMessage>();

		client.ProtocolMessageReceived += messages.Add;

		transport.When(x => x.WriteLineAsync("uci", Arg.Any<CancellationToken>()))
				 .Do(async _ =>
					 {
						 await channel.Writer.WriteAsync("id name Test Engine");
						 await channel.Writer.WriteAsync("id author Bezoro");
						 await channel.Writer.WriteAsync("option name Ponder type check default false");
						 await channel.Writer.WriteAsync("uciok");
					 }
				 );

		transport.When(x => x.WriteLineAsync("isready", Arg.Any<CancellationToken>()))
				 .Do(_ => channel.Writer.TryWrite("readyok"));

		await client.StartAsync(CancellationToken.None);

		messages.OfType<UciIdMessage>().Should().ContainSingle(m =>
																   m.Kind == UciIdKind.Name && m.Value == "Test Engine"
		);

		messages.OfType<UciIdMessage>().Should().ContainSingle(m =>
																   m.Kind == UciIdKind.Author && m.Value == "Bezoro"
		);

		messages.OfType<UciOptionMessage>().Should().ContainSingle(m => m.Option.Name == "Ponder");
		messages.OfType<UciUciOkMessage>().Should().ContainSingle();
		messages.OfType<UciReadyOkMessage>().Should().ContainSingle();

		await client.DisposeAsync();
	}
}
