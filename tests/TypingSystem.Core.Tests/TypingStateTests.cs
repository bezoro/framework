using FluentAssertions;
using JetBrains.Annotations;

namespace TypingSystem.Core.Tests;

[TestSubject(typeof(TypingState))]
public static class TypingStateTests
{
	public static class UnitTests
	{
		public class ConstructorTests
		{
			[Fact]
			public void WhenCorrectCountIsGreaterThanPositionPlus1_ShouldThrow()
			{
				const byte POSITION      = 4;
				const byte CORRECT_COUNT = 6;
				const ushort MISTAKE_COUNT = 0;

				var action = () => new TypingState(POSITION, CORRECT_COUNT, MISTAKE_COUNT);

				action.Should().Throw<ArgumentOutOfRangeException>();
			}

			[Fact]
			public void WhenMistakeCountIsGreaterThanCorrectCount_ShouldWork()
			{
				const byte POSITION = 0;
				const byte   CORRECT_COUNT = 0;
				const ushort MISTAKE_COUNT = 1;

				var state = new TypingState(POSITION, CORRECT_COUNT, MISTAKE_COUNT);

				state.Position.Should().Be(POSITION);
				state.CorrectCount.Should().Be(CORRECT_COUNT);
				state.MistakeCount.Should().Be(MISTAKE_COUNT);
			}

			[Fact]
			public void WhenMistakeCountIsGreaterThanPosition_ShouldWork()
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
			public void WhenPositionIsGreaterThanCorrectCount_ShouldThrow()
			{
				const byte POSITION      = 3;
				const byte CORRECT_COUNT = 2;
				const ushort MISTAKE_COUNT = 0;

				var action = () => new TypingState(POSITION, CORRECT_COUNT, MISTAKE_COUNT);

				action.Should().Throw<ArgumentOutOfRangeException>();
			}


			[Fact]
			public void WhenValidInputs_ShouldPopulateFields()
			{
				const byte   POSITION      = 0;
				const byte   CORRECT_COUNT = 0;
				const ushort MISTAKE_COUNT = 0;

				var state = new TypingState(POSITION, CORRECT_COUNT, MISTAKE_COUNT);

				state.Position.Should().Be(POSITION);
				state.CorrectCount.Should().Be(CORRECT_COUNT);
				state.MistakeCount.Should().Be(MISTAKE_COUNT);
			}
		}

		public static class FactoryTests
		{
			public class InitialTests
			{
				[Fact]
				public void WhenCalled_ShouldReturnInitialState()
				{
					var state = TypingState.Initial;

					state.Position.Should().Be(0);
					state.CorrectCount.Should().Be(0);
					state.MistakeCount.Should().Be(0);
				}
			}
		}

		public class IsCompletedTests
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
	}
}
