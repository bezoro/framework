using Bezoro.TypingSystem.Types;
using Bezoro.TypingSystem.Utilities;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.TypingSystem.Tests.Types;

[TestSubject(typeof(TypingValidatorOptions))]
public class TypingValidatorOptionsTests
{
	[Fact]
	public void ValidateInput_WhenOnValidatedIsConfigured_ShouldInvokeForEachValidation()
	{
		var invocationCount = 0;
		var options = new TypingValidatorOptions
		{
			OnValidated = _ => invocationCount++
		};

		TypingValidator.ValidateInput("abc".AsSpan(), 0, 'a', options);
		TypingValidator.ValidateInput("abc".AsSpan(), 1, 'x', options);
		TypingValidator.ValidateInput("abc".AsSpan(), 2, 'c', options);
		TypingValidator.ValidateInput("abc".AsSpan(), 9, 'z', options);

		invocationCount.Should().Be(4);
	}

	[Fact]
	public void ValidateInput_WhenMatchOccurs_ShouldInvokeOnMatchOnly()
	{
		var matchInvocations     = 0;
		var mismatchInvocations  = 0;
		var completedInvocations = 0;
		var faultInvocations     = 0;
		var options = new TypingValidatorOptions
		{
			OnMatch     = _ => matchInvocations++,
			OnMismatch  = _ => mismatchInvocations++,
			OnCompleted = _ => completedInvocations++,
			OnFault     = _ => faultInvocations++
		};

		TypingValidator.ValidateInput("abc".AsSpan(), 0, 'a', options);

		matchInvocations.Should().Be(1);
		mismatchInvocations.Should().Be(0);
		completedInvocations.Should().Be(0);
		faultInvocations.Should().Be(0);
	}

	[Fact]
	public void ValidateInput_WhenCompletedOccurs_ShouldInvokeOnCompletedOnly()
	{
		var matchInvocations     = 0;
		var mismatchInvocations  = 0;
		var completedInvocations = 0;
		var faultInvocations     = 0;
		var options = new TypingValidatorOptions
		{
			OnMatch     = _ => matchInvocations++,
			OnMismatch  = _ => mismatchInvocations++,
			OnCompleted = _ => completedInvocations++,
			OnFault     = _ => faultInvocations++
		};

		TypingValidator.ValidateInput("abc".AsSpan(), 2, 'c', options);

		matchInvocations.Should().Be(0);
		mismatchInvocations.Should().Be(0);
		completedInvocations.Should().Be(1);
		faultInvocations.Should().Be(0);
	}

	[Fact]
	public void ValidateInput_WhenMismatchOccurs_ShouldInvokeOnMismatchOnly()
	{
		var matchInvocations     = 0;
		var mismatchInvocations  = 0;
		var completedInvocations = 0;
		var faultInvocations     = 0;
		var options = new TypingValidatorOptions
		{
			OnMatch     = _ => matchInvocations++,
			OnMismatch  = _ => mismatchInvocations++,
			OnCompleted = _ => completedInvocations++,
			OnFault     = _ => faultInvocations++
		};

		TypingValidator.ValidateInput("abc".AsSpan(), 1, 'x', options);

		matchInvocations.Should().Be(0);
		mismatchInvocations.Should().Be(1);
		completedInvocations.Should().Be(0);
		faultInvocations.Should().Be(0);
	}

	[Fact]
	public void ValidateInput_WhenFaultOccurs_ShouldInvokeOnFault()
	{
		var faultInvocations = 0;
		var options = new TypingValidatorOptions
		{
			OnFault = _ => faultInvocations++
		};

		TypingValidator.ValidateInput(ReadOnlySpan<char>.Empty, 0, 'x', options);
		TypingValidator.ValidateInput("abc".AsSpan(),          9, 'x', options);

		faultInvocations.Should().Be(2);
	}

	[Fact]
	public void ValidateInput_WhenMetricsAreConfigured_ShouldUpdateMetricsThroughOptions()
	{
		var metrics = new TypingMetrics();
		var options = new TypingValidatorOptions
		{
			Metrics = metrics
		};

		TypingValidator.ValidateInput("abc".AsSpan(),          0, 'a', options);
		TypingValidator.ValidateInput("abc".AsSpan(),          1, 'x', options);
		TypingValidator.ValidateInput(ReadOnlySpan<char>.Empty, 0, 'x', options);

		metrics.TotalInputs.Should().Be(3);
		metrics.CorrectInputs.Should().Be(1);
		metrics.MistakeInputs.Should().Be(1);
		metrics.FaultedInputs.Should().Be(1);
	}

	[Fact]
	public void ValidateInput_WhenOptionsAreNull_ShouldNotThrow()
	{
		Action action = () => _ = TypingValidator.ValidateInput("abc".AsSpan(), 0, 'a', null);

		action.Should().NotThrow();
	}
}
