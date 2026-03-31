using Bezoro.Chess.UCI.Protocol.API.Common.Extensions;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.Chess.UCI.Protocol.Tests.API.Common.Extensions;

[TestSubject(typeof(MoveEvaluationExtensions))]
public class MoveEvaluationExtensionsTests
{
	[Fact]
	public void ToDebugDisplayString_WhenClassificationIsResolved_ShouldIncludeTypeTags()
	{
		var evaluation = new MoveEvaluation(
			"e7f8q",
			new(120, null),
			MoveClassification.CreateStructural(
				MoveClassificationFlags.Capture | MoveClassificationFlags.Promotion,
				'P',
				'r',
				'q'
			).WithTacticalOutcome(true, false, false)
		);

		string line = evaluation.ToDebugDisplayString();

		line.Should().Contain("e7f8q");
		line.Should().Contain("+120 cp");
		line.Should().Contain("[capture,promotion=q,check]");
	}
}
