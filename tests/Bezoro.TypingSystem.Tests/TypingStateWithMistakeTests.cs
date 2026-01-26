using Bezoro.TypingSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.TypingSystem.Tests;

[TestSubject(typeof(TypingState))]
public class TypingStateWithMistakeTests
{
	[Fact]
	public void WhenCalled_ShouldIncrementMistakeCount()
	{
		var state = new TypingState(0, 0, 0);

		var updated = state.WithMistake();

		updated.MistakeCount.Should().Be(1);
		updated.Position.Should().Be(state.Position);
		updated.CorrectCount.Should().Be(state.CorrectCount);
	}

	[Fact]
	public void WhenMistakeCountAtMaximum_ShouldThrow()
	{
		var state = new TypingState(0, 0, ushort.MaxValue);

		Action act = () => state.WithMistake();

		act.Should()
		   .Throw<InvalidOperationException>()
		   .WithMessage("Mistake count cannot exceed 65535.");
	}
}
