using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.Chess.UCI.Protocol.Tests.API.Types;

[TestSubject(typeof(PlayableMatchSetup))]
public sealed class PlayableMatchSetupTests
{
	[Fact]
	public void ResolveCurrentFen_WhenPlayedMovesAreProvided_ShouldReturnEffectivePosition()
	{
		var setup = new PlayableMatchSetup(Fen.Default, ["e2e4", "e7e5"]);

		var currentFen = setup.ResolveCurrentFen();

		currentFen.ActiveColor.Should().Be('w');
		currentFen.FullmoveNumber.Should().Be(2);
		setup.PlayedMoves.Should().Equal(["e2e4", "e7e5"]);
	}

	[Fact]
	public void Validate_WhenPlayedMoveIsIllegal_ShouldThrowArgumentException()
	{
		var setup = new PlayableMatchSetup(Fen.Default, ["e2e5"]);

		setup.Invoking(static value => value.Validate())
			 .Should()
			 .Throw<ArgumentException>();
	}

	[Fact]
	public void Constructor_WhenMovesAreMixedCase_ShouldNormalizePlayedMoves()
	{
		var setup = new PlayableMatchSetup(Fen.Default, ["E2E4", "E7E5"]);

		setup.PlayedMoves.Should().Equal(["e2e4", "e7e5"]);
	}

	[Fact]
	public void Standard_ShouldRepresentBrandNewStandardMatch()
	{
		var setup = PlayableMatchSetup.Standard;

		setup.BaseFen.Should().Be(Fen.Default);
		setup.PlayedMoves.Should().BeEmpty();
		setup.Clock.Should().BeNull();
	}
}
