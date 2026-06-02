using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.Chess.UCI.Protocol.Tests.API.Types;

[TestSubject(typeof(PlayableMatchClockSetup))]
public sealed class PlayableMatchClockSetupTests
{
	[Fact]
	public void Validate_WhenTimesAreNonNegative_ShouldNotThrow()
	{
		var setup = new PlayableMatchClockSetup(
			TimeSpan.FromMinutes(3),
			TimeSpan.FromMinutes(2),
			TimeSpan.FromSeconds(1));

		setup.Invoking(static value => value.Validate())
			 .Should()
			 .NotThrow();
	}

	[Fact]
	public void Validate_WhenRemainingTimeIsNegative_ShouldThrowArgumentOutOfRangeException()
	{
		var setup = new PlayableMatchClockSetup(
			TimeSpan.FromSeconds(-1),
			TimeSpan.FromMinutes(2));

		setup.Invoking(static value => value.Validate())
			 .Should()
			 .Throw<ArgumentOutOfRangeException>();
	}

	[Fact]
	public void FromExactRestore_WhenSnapshotIsProvided_ShouldPreserveAdvancedClockDetails()
	{
		var restore = new PlayableMatchClockRestore(
			TimeSpan.FromSeconds(20),
			TimeSpan.FromSeconds(15),
			'b',
			TimeSpan.FromSeconds(1),
			true,
			3,
			2,
			1,
			DateTimeOffset.UtcNow);

		var setup = PlayableMatchClockSetup.FromExactRestore(restore);

		setup.ExactRestore.Should().Be(restore);
		setup.WhiteRemaining.Should().Be(restore.WhiteRemaining);
		setup.BlackRemaining.Should().Be(restore.BlackRemaining);
		setup.DelayRemaining.Should().Be(restore.DelayRemaining);
		setup.IsPaused.Should().BeTrue();
	}
}
