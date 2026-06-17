using Bezoro.Chess.UCI.Protocol.API.Common.Extensions;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.Chess.UCI.Protocol.Tests.API.Common.Extensions;

[TestSubject(typeof(UciMoveExtensions))]
public sealed class UciMoveExtensionsTests
{
	[Fact]
	public void TryNormalizeCoordinateMove_WhenMoveIsValid_ShouldReturnNormalizedNotation()
	{
		const string move = " A7A8Q ";

		var normalized = move.TryNormalizeCoordinateMove(out string normalizedMove);

		normalized.Should().BeTrue();
		normalizedMove.Should().Be("a7a8q");
	}

	[Fact]
	public void TryNormalizeCoordinateMove_WhenMoveHasInvalidPromotionTravel_ShouldReturnFalse()
	{
		const string move = "e2e4q";

		var normalized = move.TryNormalizeCoordinateMove(out string normalizedMove);

		normalized.Should().BeFalse();
		normalizedMove.Should().BeEmpty();
	}
}
