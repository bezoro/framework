using Bezoro.TypingSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.TypingSystem.Tests;

[TestSubject(typeof(TypingState))]
public class TypingStateWithCorrectTests
{
	[Fact]
	public void WhenCalled_ShouldAdvancePositionAndCorrectCount()
	{
		var state = new TypingState(0, 0, 0);

		var updated = state.WithCorrect();

		updated.Position.Should().Be(1);
		updated.CorrectCount.Should().Be(1);
		updated.MistakeCount.Should().Be(state.MistakeCount);
	}

	[Fact]
	public void WhenCorrectCountAtMax_ShouldThrow()
	{
		var state = new TypingState(byte.MaxValue - 1, byte.MaxValue, 0);

		Action act = () => state.WithCorrect();

		act.Should()
		   .Throw<InvalidOperationException>()
		   .WithMessage("Correct count cannot exceed 255.");
	}

	[Fact]
	public void WhenPositionAtMax_ShouldThrow()
	{
		var state = new TypingState(byte.MaxValue, byte.MaxValue, 0);

		Action act = () => state.WithCorrect();

		act.Should()
		   .Throw<InvalidOperationException>()
		   .WithMessage("Position cannot exceed 255.");
	}
}
