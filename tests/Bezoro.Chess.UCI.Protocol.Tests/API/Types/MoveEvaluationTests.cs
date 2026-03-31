using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.Chess.UCI.Protocol.Tests.API.Types;

[TestSubject(typeof(MoveEvaluation))]
public class MoveEvaluationTests
{
	[Fact]
	public void Display_WhenMoveEvaluationHasCentipawnScore_ShouldUseAbsolutePlayerRelativeScore()
	{
		var evaluation = new MoveEvaluation("e2e4", new(22, null));

		evaluation.Display.Should().Be("+22 cp");
		evaluation.SortValue.Should().Be(22);
	}

	[Fact]
	public void Display_WhenMoveEvaluationHasMateScore_ShouldPreferMateNotation()
	{
		var evaluation = new MoveEvaluation("e2e4", new(null, 3));

		evaluation.Display.Should().Be("+M3");
		evaluation.SortValue.Should().BeGreaterThan(99_000);
	}
}
