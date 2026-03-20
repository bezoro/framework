using Bezoro.Chess.UCI.Protocol.API.Types;
using Bezoro.Chess.UCI.Protocol.API;
using FluentAssertions;
using JetBrains.Annotations;
using NSubstitute;

namespace Bezoro.Chess.UCI.Protocol.Tests.Domain;

[TestSubject(typeof(UciEngineClient))]
public class UciEngineClientMetadataTests
{
	[Fact]
	public async Task StartAsync_WhenHandshakeEmitsIdAndOptionLines_ShouldCaptureEngineMetadata()
	{
		var (transport, channel) = UciEngineClientTestHelpers.CreateMockTransport();
		var client = new UciEngineClient(transport);

		transport.When(x => x.WriteLineAsync("uci", Arg.Any<CancellationToken>()))
				 .Do(async _ =>
					 {
						 await channel.Writer.WriteAsync("id name Test Engine 1.0");
						 await channel.Writer.WriteAsync("id author Bezoro");
						 await channel.Writer.WriteAsync(
							 "option name Hash type spin default 16 min 1 max 4096"
						 );
						 await channel.Writer.WriteAsync(
							 "option name Ponder type check default false"
						 );
						 await channel.Writer.WriteAsync(
							 "option name Style type combo default Normal var Normal var Risky"
						 );
						 await channel.Writer.WriteAsync("uciok");
					 }
				 );
		transport.When(x => x.WriteLineAsync("isready", Arg.Any<CancellationToken>()))
				 .Do(_ => channel.Writer.TryWrite("readyok"));

		await client.StartAsync(CancellationToken.None);

		client.EngineInfo.Name.Should().Be("Test Engine 1.0");
		client.EngineInfo.Author.Should().Be("Bezoro");
		client.AvailableOptions.Should().ContainSingle(x =>
			x.Name == "Hash" &&
			x.Type == UciOptionType.Spin &&
			x.DefaultValue == "16" &&
			x.Min == 1 &&
			x.Max == 4096);
		client.AvailableOptions.Should().ContainSingle(x =>
			x.Name == "Style" &&
			x.Type == UciOptionType.Combo &&
			x.DefaultValue == "Normal" &&
			x.Variables.SequenceEqual(new[] { "Normal", "Risky" }));
		client.Capabilities.DebugCommand.Should().Be(UciCapabilityState.Supported);
		client.Capabilities.RegisterCommand.Should().Be(UciCapabilityState.Supported);
		client.Capabilities.PonderHit.Should().Be(UciCapabilityState.Supported);

		await client.DisposeAsync();
	}
}
