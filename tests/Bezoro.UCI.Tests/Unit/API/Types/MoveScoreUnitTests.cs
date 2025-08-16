using Bezoro.UCI.API.Types;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.UCI.Tests.Unit.API.Types;

[TestSubject(typeof(MoveScore))]
public class MoveScoreUnitTests
{
	[Fact]
	public void TryParse_WhenInvalidInput_ReturnsFalseAndNull()
	{
		// Act
		var  line    = "invalid";
		bool success = MoveScore.TryParse(line, out var result);

		// Assert
		success.Should().BeFalse();
		result.Should().BeNull();
	}

	[Fact]
	public void TryParse_WhenValidInput_ReturnsTrueAndValidObject()
	{
		// Arrange
		var line =
			"info depth 12 seldepth 20 multipv 1 score cp 34 nodes 1000 nps 5000 tbhits 2 time 50 pv e2e4 e7e5 g1f3";

		// Act
		bool success = MoveScore.TryParse(line, out var moveScore);

		success.Should().BeTrue();
		moveScore.Should().NotBeNull();
		moveScore!.Value.ScoreCp.Should().Be(34);
	}
}
