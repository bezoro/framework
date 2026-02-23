using Bezoro.TypingSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.TypingSystem.Tests.Types;

[TestSubject(typeof(TypingState))]
public class TypingStateConstructorTests
{
	[Fact]
	public void Constructor_WhenCorrectCountIsGreaterThanPositionPlusOne_ShouldThrowArgumentOutOfRangeException()
	{
		const byte   POSITION      = 4;
		const byte   CORRECT_COUNT = 6;
		const ushort MISTAKE_COUNT = 0;

		var action = () => new TypingState(POSITION, CORRECT_COUNT, MISTAKE_COUNT);

		action.Should().Throw<ArgumentOutOfRangeException>();
	}

	[Fact]
	public void Constructor_WhenInputsAreValid_ShouldPopulateFields()
	{
		const byte   POSITION      = 0;
		const byte   CORRECT_COUNT = 0;
		const ushort MISTAKE_COUNT = 0;

		var state = new TypingState(POSITION, CORRECT_COUNT, MISTAKE_COUNT);

		state.Position.Should().Be(POSITION);
		state.CorrectCount.Should().Be(CORRECT_COUNT);
		state.MistakeCount.Should().Be(MISTAKE_COUNT);
	}

	[Fact]
	public void Constructor_WhenMistakeCountIsGreaterThanCorrectCount_ShouldCreateState()
	{
		const byte   POSITION      = 0;
		const byte   CORRECT_COUNT = 0;
		const ushort MISTAKE_COUNT = 1;

		var state = new TypingState(POSITION, CORRECT_COUNT, MISTAKE_COUNT);

		state.Position.Should().Be(POSITION);
		state.CorrectCount.Should().Be(CORRECT_COUNT);
		state.MistakeCount.Should().Be(MISTAKE_COUNT);
	}

	[Fact]
	public void Constructor_WhenMistakeCountIsGreaterThanPosition_ShouldCreateState()
	{
		const byte   POSITION      = 0;
		const byte   CORRECT_COUNT = 0;
		const ushort MISTAKE_COUNT = 1;

		var state = new TypingState(POSITION, CORRECT_COUNT, MISTAKE_COUNT);

		state.Position.Should().Be(POSITION);
		state.CorrectCount.Should().Be(CORRECT_COUNT);
		state.MistakeCount.Should().Be(MISTAKE_COUNT);
	}

	[Fact]
	public void Constructor_WhenPositionIsGreaterThanCorrectCount_ShouldThrowArgumentOutOfRangeException()
	{
		const byte   POSITION      = 3;
		const byte   CORRECT_COUNT = 2;
		const ushort MISTAKE_COUNT = 0;

		var action = () => new TypingState(POSITION, CORRECT_COUNT, MISTAKE_COUNT);

		action.Should().Throw<ArgumentOutOfRangeException>();
	}
}
