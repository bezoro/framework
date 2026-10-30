using Bezoro.Chess.UCI.Protocol.Domain.Common.Helpers;
using FluentAssertions;
using NSubstitute;

namespace Bezoro.Chess.UCI.Protocol.Tests.Domain;

public class UciCounterWidthTests
{
	private const string WideInfo = "info depth 12 score cp 25 nodes 18446744073709551615 nps 5000000000 tbhits 6000000000 time 7000000000 pv e2e4 e7e5";

	[Fact]
	public void Parser_ShouldPreserveUnsigned64BitCountersInTypedPayloadAndPv()
	{
		UciProtocolParser.TryParse(WideInfo, out var message).Should().BeTrue();
		var info = message.Info!.Value.Payload;
		info.Nodes.Should().Be(ulong.MaxValue);
		info.Nps.Should().Be(5_000_000_000UL);
		info.TbHits.Should().Be(6_000_000_000UL);
		info.Time.Should().Be(7_000_000_000UL);
		var pv = info.PrincipalVariation!.Value;
		pv.Nodes.Should().Be(ulong.MaxValue);
		pv.Nps.Should().Be(5_000_000_000UL);
		pv.TbHits.Should().Be(6_000_000_000UL);
		pv.Time.Should().Be(7_000_000_000UL);
	}

	[Fact]
	public void SearchResult_ShouldKeep64BitCumulativeTotalsWithoutOverflow()
	{
		SearchResult.TryParse([WideInfo, WideInfo, "bestmove e2e4"], out var result).Should().BeTrue();
		result.TotalNodesSearched.Should().Be(ulong.MaxValue);
		result.TotalTbHits.Should().Be(6_000_000_000UL);
		result.TotalSearchTimeMs.Should().Be(7_000_000_000UL);
		result.BestPv!.Value.Nps.Should().Be(5_000_000_000UL);
		result.Deconstruct(out _, out _, out _, out ulong nodes, out ulong hits, out ulong milliseconds, out _, out _, out _);
		nodes.Should().Be(ulong.MaxValue);
		hits.Should().Be(6_000_000_000UL);
		milliseconds.Should().Be(7_000_000_000UL);
	}

	[Theory]
	[InlineData("18446744073709551616")]
	[InlineData("-1")]
	[InlineData("1.5")]
	public void Parser_ShouldIgnoreInvalidCountersAndPreserveOtherEvidence(string invalid)
	{
		UciProtocolParser.TryParse($"info nodes {invalid} nps {invalid} time {invalid} tbhits {invalid} depth 6 pv e2e4", out var message)
			.Should().BeTrue();
		var info = message.Info!.Value.Payload;
		info.Nodes.Should().BeNull();
		info.Nps.Should().BeNull();
		info.Time.Should().BeNull();
		info.TbHits.Should().BeNull();
		info.Depth.Should().Be(6);
		info.PrincipalVariation!.Value.Moves.Should().Equal("e2e4");
	}

	[Fact]
	public async Task ClientSearch_ShouldReturn64BitCountersFromTheLiveProtocolStream()
	{
		var (transport, channel) = UciEngineClientTestHelpers.CreateMockTransport();
		transport.WriteLineAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
		{
			string command = (string)call[0];
			if (command == "uci") channel.Writer.TryWrite("uciok");
			if (command == "isready") channel.Writer.TryWrite("readyok");
			if (command == "go depth 12")
			{
				channel.Writer.TryWrite(WideInfo);
				channel.Writer.TryWrite("info nodes 18446744073709551615 time 8000000000 tbhits 9000000000");
				channel.Writer.TryWrite("bestmove e2e4");
			}
			return Task.CompletedTask;
		});
		await using var client = new UciEngineClient(transport);
		await client.StartAsync();
		var result = await client.GoAsync(new() { Depth = 12 }, default).WaitAsync(TimeSpan.FromSeconds(2));
		result.TotalNodesSearched.Should().Be(ulong.MaxValue);
		result.TotalTbHits.Should().Be(9_000_000_000UL);
		result.TotalSearchTimeMs.Should().Be(8_000_000_000UL);
	}
}
