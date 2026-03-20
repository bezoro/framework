using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.Chess.UCI.Protocol.Tests.API.Types;

[TestSubject(typeof(PrincipalVariation))]
public class PrincipalVariationTests
{
	[Fact]
	public void TryParse_WhenWithCpScore_ShouldParseFieldsAndMoves()
	{
		const string LINE =
			"info depth 12 seldepth 20 multipv 1 score cp 34 nodes 123456 nps 2500000 tbhits 0 time 123 pv e2e4 e7e5 g1f3";

		bool ok = PrincipalVariation.TryParse(LINE, out var pv);

		ok.Should().BeTrue();
		pv.Depth.Should().Be(12u);
		pv.SelDepth.Should().Be(20u);
		pv.MultiPv.Should().Be(1u);
		pv.ScoreCp.Should().Be(34);
		pv.ScoreMate.Should().BeNull();
		pv.Nodes.Should().Be(123456u);
		pv.Nps.Should().Be(2500000u);
		pv.TbHits.Should().Be(0u);
		pv.Time.Should().Be(123u);
		pv.Moves.Should().ContainInOrder("e2e4", "e7e5", "g1f3");
		pv.RawPv.Should().Be("e2e4 e7e5 g1f3");
	}

	[Fact]
	public void TryParse_WhenWithMateScore_ShouldParseMateAndNoCp()
	{
		const string LINE =
			"info depth 8 seldepth 12 multipv 2 score mate -3 nodes 50000 tbhits 0 time 1500 pv e2e4 e7e5";

		bool ok = PrincipalVariation.TryParse(LINE, out var pv);

		ok.Should().BeTrue();
		pv.ScoreMate.Should().Be(-3);
		pv.ScoreCp.Should().BeNull();
		pv.Moves.Should().ContainInOrder("e2e4", "e7e5");
	}

	[Fact]
	public void TryParse_WhenWithoutPv_ShouldReturnFalse()
	{
		const string LINE = "info depth 10 seldepth 10 multipv 1 score cp 10 nodes 1";

		bool ok = PrincipalVariation.TryParse(LINE, out var pv);

		ok.Should().BeFalse();
		pv.Moves.Should().BeNullOrEmpty();
	}
}
