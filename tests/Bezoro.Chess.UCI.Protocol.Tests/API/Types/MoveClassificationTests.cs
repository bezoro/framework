using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.Chess.UCI.Protocol.Tests.API.Types;

[TestSubject(typeof(MoveClassification))]
public class MoveClassificationTests
{
	[Fact]
	public void WithTacticalOutcome_WhenCheckmateIsResolved_ShouldSetResolvedFlags()
	{
		var classification = MoveClassification.CreateStructural(
			MoveClassificationFlags.Promotion | MoveClassificationFlags.Capture,
			movingPiece: 'P',
			capturedPiece: 'r',
			promotionPiece: 'q'
		);

		var resolved = classification.WithTacticalOutcome(isCheck: true, isMate: true, isStalemate: false);

		resolved.IsResolved.Should().BeTrue();
		resolved.IsPromotion.Should().BeTrue();
		resolved.IsCapture.Should().BeTrue();
		resolved.IsCheck.Should().BeTrue();
		resolved.IsMate.Should().BeTrue();
		resolved.IsStalemate.Should().BeFalse();
	}
}
