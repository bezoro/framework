using Bezoro.TypingSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.TypingSystem.Tests;

[TestSubject(typeof(TypingState))]
public class TypingStateIsCompleteTests
{
	[Fact]
	public void WhenTargetLengthIsLessThanPosition_ShouldThrow()
	{
		var state = new TypingState(4, 5, 0);

		var action = () => state.IsComplete(3);

		action.Should().Throw<ArgumentOutOfRangeException>();
	}

	[Fact]
	public void WhenTargetLengthLessThanCorrectCount_ShouldThrow()
	{
		var state = new TypingState(4, 5, 0);

		var action = () => state.IsComplete(4);

		action.Should().Throw<ArgumentOutOfRangeException>();
	}

	[Fact]
	public void WhenTargetLengthNotReached_ShouldReturnFalse()
	{
		var state = new TypingState(3, 4, 0);

		state.IsComplete(5).Should().BeFalse();
	}

	[Fact]
	public void WhenTargetLengthReached_ShouldReturnTrue()
	{
		var state = new TypingState(4, 5, 0);

		state.IsComplete(5).Should().BeTrue();
	}
}
