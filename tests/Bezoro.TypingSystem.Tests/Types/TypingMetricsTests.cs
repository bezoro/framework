using Bezoro.TypingSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.TypingSystem.Tests.Types;

[TestSubject(typeof(TypingMetrics))]
public class TypingMetricsTests
{
	[Fact]
	public void Record_WhenResultIsMatchOrCompleted_ShouldIncrementCorrectInputs()
	{
		var metrics = new TypingMetrics();

		metrics.Record(TypingResult.Match('a', 0, 'a', 3));
		metrics.Record(TypingResult.Completed('c', 2, 'c', 3));

		metrics.CorrectInputs.Should().Be(2);
		metrics.MistakeInputs.Should().Be(0);
		metrics.FaultedInputs.Should().Be(0);
		metrics.TotalInputs.Should().Be(2);
	}

	[Fact]
	public void Record_WhenResultIsMismatch_ShouldIncrementMistakeInputs()
	{
		var metrics = new TypingMetrics();

		metrics.Record(TypingResult.Mismatch('a', 0, 'z', 3));

		metrics.MistakeInputs.Should().Be(1);
		metrics.CorrectInputs.Should().Be(0);
		metrics.FaultedInputs.Should().Be(0);
		metrics.TotalInputs.Should().Be(1);
	}

	[Fact]
	public void Record_WhenResultIsFaulted_ShouldIncrementFaultedInputsOnly()
	{
		var metrics = new TypingMetrics();

		metrics.Record(TypingResult.EmptyTarget(0, 'x'));

		metrics.FaultedInputs.Should().Be(1);
		metrics.CorrectInputs.Should().Be(0);
		metrics.MistakeInputs.Should().Be(0);
		metrics.TotalInputs.Should().Be(1);
	}

	[Fact]
	public void TotalEvaluated_WhenFaultsExist_ShouldExcludeFaultedInputs()
	{
		var metrics = new TypingMetrics();
		metrics.Record(TypingResult.Match('a', 0, 'a', 3));
		metrics.Record(TypingResult.EmptyTarget(0, 'x'));

		metrics.TotalInputs.Should().Be(2);
		metrics.TotalEvaluated.Should().Be(1);
	}

	[Fact]
	public void Accuracy_WhenNoEvaluatedInputs_ShouldReturnZero()
	{
		var metrics = new TypingMetrics();

		metrics.Accuracy.Should().Be(0d);
	}

	[Fact]
	public void Accuracy_WhenCorrectAndMistakeInputsExist_ShouldReturnExpectedRatio()
	{
		var metrics = new TypingMetrics();
		metrics.Record(TypingResult.Match('a', 0, 'a', 3));
		metrics.Record(TypingResult.Mismatch('b', 1, 'x', 3));
		metrics.Record(TypingResult.EmptyTarget(0, 'x'));

		metrics.Accuracy.Should().Be(0.5d);
	}

	[Fact]
	public void CharactersPerMinute_WhenNoCorrectInputs_ShouldReturnZero()
	{
		var metrics = new TypingMetrics();

		metrics.CharactersPerMinute.Should().Be(0d);
	}

	[Fact]
	public void Reset_WhenCalled_ShouldClearCountersAndRestartElapsedTime()
	{
		var metrics = new TypingMetrics();
		metrics.Record(TypingResult.Match('a', 0, 'a', 3));
		Thread.Sleep(20);
		var elapsedBeforeReset = metrics.Elapsed;

		metrics.Reset();

		metrics.TotalInputs.Should().Be(0);
		metrics.CorrectInputs.Should().Be(0);
		metrics.MistakeInputs.Should().Be(0);
		metrics.FaultedInputs.Should().Be(0);
		metrics.Elapsed.Should().BeLessThan(elapsedBeforeReset);
	}
}
