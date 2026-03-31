using Bezoro.Chess.UCI.Protocol.API.Common.Helpers;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.Chess.UCI.Protocol.Tests.Domain;

[TestSubject(typeof(AdvantageScale))]
public class AdvantageScaleTests
{
	[Theory]
	[InlineData(0, 0.00, 0.00)]
	[InlineData(100, 0.08, 0.15)]
	[InlineData(300, 0.20, 0.35)]
	[InlineData(500, 0.35, 0.55)]
	[InlineData(1000, 0.60, 0.80)]
	public void NormalizeCp_WhenPositionIsWinning_ShouldScaleModerately(int cp, double minExpected, double maxExpected)
	{
		double normalized = AdvantageScale.NormalizeCp(cp);

		normalized.Should().BeInRange(minExpected, maxExpected);
	}

	[Fact]
	public void NormalizeCp_WhenScoresHaveOppositeSigns_ShouldRemainSymmetric()
	{
		double positive = AdvantageScale.NormalizeCp(500);
		double negative = AdvantageScale.NormalizeCp(-500);

		negative.Should().BeApproximately(-positive, 0.0001);
	}
}
