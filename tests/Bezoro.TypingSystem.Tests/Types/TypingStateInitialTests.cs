using Bezoro.TypingSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.TypingSystem.Tests.Types;

[TestSubject(typeof(TypingState))]
public class TypingStateInitialTests
{
	[Fact]
	public void Initial_WhenAccessed_ShouldReturnInitialState()
	{
		var state = TypingState.Initial;

		state.Position.Should().Be(0);
		state.CorrectCount.Should().Be(0);
		state.MistakeCount.Should().Be(0);
	}
}
