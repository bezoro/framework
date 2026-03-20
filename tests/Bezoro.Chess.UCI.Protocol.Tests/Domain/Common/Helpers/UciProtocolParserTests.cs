using Bezoro.Chess.UCI.Protocol.Domain.Common.Helpers;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.Chess.UCI.Protocol.Tests.Domain.Common.Helpers;

[TestSubject(typeof(UciProtocolParser))]
public class UciProtocolParserTests
{
	[Fact]
	public void TryParse_WhenBestMoveLineIsProvided_ShouldReturnTypedMessage()
	{
		const string LINE = "bestmove e2e4 ponder e7e5";

		bool parsed = UciProtocolParser.TryParse(LINE, out var message);

		parsed.Should().BeTrue();
		message.Type.Should().Be(UciProtocolMessageType.BestMove);
		message.BestMove.HasValue.Should().BeTrue();
		var bestMove = message.BestMove!.Value;
		bestMove.BestMove.Should().Be("e2e4");
		bestMove.PonderMove.Should().Be("e7e5");
		bestMove.RawLine.Should().Be(LINE);
	}

	[Fact]
	public void TryParse_WhenCopyProtectionLineIsProvided_ShouldReturnTypedMessage()
	{
		bool parsed = UciProtocolParser.TryParse("copyprotection checking", out var message);

		parsed.Should().BeTrue();
		message.CopyProtection.HasValue.Should().BeTrue();
		message.CopyProtection!.Value.State.Should().Be(UciProtectionState.Checking);
	}

	[Fact]
	public void TryParse_WhenIdLineIsProvided_ShouldReturnTypedMessage()
	{
		bool parsed = UciProtocolParser.TryParse("id name Stockfish 17", out var message);

		parsed.Should().BeTrue();
		message.Id.HasValue.Should().BeTrue();
		var idMessage = message.Id!.Value;
		idMessage.Kind.Should().Be(UciIdKind.Name);
		idMessage.Value.Should().Be("Stockfish 17");
	}

	[Fact]
	public void TryParse_WhenInfoLineContainsRichFields_ShouldPopulateTypedPayload()
	{
		const string LINE =
			"info depth 20 seldepth 32 multipv 2 score cp 34 lowerbound nodes 12345 nps 456789 hashfull 12 cpuload 876 time 250 tbhits 4 currmove e2e4 currmovenumber 7 refutation d2d4 d7d5 currline 1 e2e4 e7e5 g1f3 pv e2e4 e7e5 g1f3";

		bool parsed = UciProtocolParser.TryParse(LINE, out var message);

		parsed.Should().BeTrue();
		message.Info.HasValue.Should().BeTrue();
		var info = message.Info!.Value.Payload;
		info.Depth.Should().Be(20u);
		info.SelDepth.Should().Be(32u);
		info.MultiPv.Should().Be(2u);
		info.Score.Should().NotBeNull();
		info.Score!.Value.Centipawns.Should().Be(34);
		info.Score.Value.Bound.Should().Be(UciScoreBound.Lower);
		info.Nodes.Should().Be(12345u);
		info.Nps.Should().Be(456789u);
		info.HashFull.Should().Be(12u);
		info.CpuLoad.Should().Be(876u);
		info.Time.Should().Be(250u);
		info.TbHits.Should().Be(4u);
		info.CurrentMove.Should().Be("e2e4");
		info.CurrentMoveNumber.Should().Be(7u);
		info.Refutation.Should().Equal("d2d4", "d7d5");
		info.CurrentLineCpu.Should().Be(1u);
		info.CurrentLine.Should().Equal("e2e4", "e7e5", "g1f3");
		info.PrincipalVariation.Should().NotBeNull();
		info.PrincipalVariation!.Value.RawPv.Should().Be("e2e4 e7e5 g1f3");
	}

	[Fact]
	public void TryParse_WhenInfoStringLineIsProvided_ShouldCaptureEngineMessage()
	{
		bool parsed = UciProtocolParser.TryParse("info string NNUE evaluation enabled", out var message);

		parsed.Should().BeTrue();
		message.Info.HasValue.Should().BeTrue();
		message.Info!.Value.Payload.String.Should().Be("NNUE evaluation enabled");
	}

	[Fact]
	public void TryParse_WhenOptionLineIsProvided_ShouldReturnTypedMessage()
	{
		bool parsed = UciProtocolParser.TryParse(
			"option name Style type combo default Normal var Normal var Aggressive",
			out var message
		);

		parsed.Should().BeTrue();
		message.Option.HasValue.Should().BeTrue();
		var option = message.Option!.Value.Option;
		option.Name.Should().Be("Style");
		option.Type.Should().Be(UciOptionType.Combo);
		option.DefaultValue.Should().Be("Normal");
		option.Variables.Should().Equal("Normal", "Aggressive");
	}

	[Fact]
	public void TryParse_WhenReadyOkLineIsProvided_ShouldReturnTypedMessage()
	{
		bool parsed = UciProtocolParser.TryParse("readyok", out var message);

		parsed.Should().BeTrue();
		message.ReadyOk.HasValue.Should().BeTrue();
	}

	[Fact]
	public void TryParse_WhenRegistrationLineIsProvided_ShouldReturnTypedMessage()
	{
		bool parsed = UciProtocolParser.TryParse("registration error", out var message);

		parsed.Should().BeTrue();
		message.Registration.HasValue.Should().BeTrue();
		message.Registration!.Value.State.Should().Be(UciProtectionState.Error);
	}

	[Fact]
	public void TryParse_WhenUciOkLineIsProvided_ShouldReturnTypedMessage()
	{
		bool parsed = UciProtocolParser.TryParse("uciok", out var message);

		parsed.Should().BeTrue();
		message.UciOk.HasValue.Should().BeTrue();
	}
}
