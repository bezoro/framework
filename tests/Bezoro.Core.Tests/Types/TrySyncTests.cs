using System;
using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(Try))]
public class TrySyncTests
{
	[Fact]
	public void DoWithNullAction_WhenCalled_ShouldThrowArgumentNullException()
	{
		var action = () => Try.Do(null!);
		action.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public void GetOrDefaultWithFactoryWithSuccessfulFunction_WhenCalled_ShouldReturnValue()
	{
		int result = Try.GetOrDefault(() => 42, () => 0);

		result.Should().Be(42);
	}

	[Fact]
	public void GetOrDefaultWithFailingFunction_WhenCalled_ShouldReturnDefault()
	{
		int result = Try.GetOrDefault(() => throw new InvalidOperationException(), 99);

		result.Should().Be(99);
	}

	[Fact]
	public void GetOrDefaultWithSuccessfulFunction_WhenCalled_ShouldReturnValue()
	{
		int result = Try.GetOrDefault(() => 42, 0);

		result.Should().Be(42);
	}

	[Fact]
	public void GetWithExceptionTransform_WhenCalled_ShouldThrowTransformedException()
	{
		var action = () => Try.Get<int>(
			() => throw new InvalidOperationException("Original"),
			ex => new ApplicationException("Transformed", ex)
		);

		action.Should().Throw<ApplicationException>()
			  .WithMessage("Transformed")
			  .WithInnerException<InvalidOperationException>()
			  .WithMessage("Original");
	}

	[Fact]
	public void GetWithNullFunction_WhenCalled_ShouldThrowArgumentNullException()
	{
		var action = () => Try.Get<int>(null!);
		action.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public void GetWithValidFunction_WhenCalled_ShouldReturnResult()
	{
		int result = Try.Get(() => 42);
		result.Should().Be(42);
	}

	[Fact]
	public void TryDoWithFailingAction_WhenCalled_ShouldReturnFalse()
	{
		bool result = Try.TryDo(() => throw new InvalidOperationException());

		result.Should().BeFalse();
	}

	[Fact]
	public void TryDoWithSuccessfulAction_WhenCalled_ShouldReturnTrue()
	{
		var  executed = false;
		bool result   = Try.TryDo(() => executed = true);

		result.Should().BeTrue();
		executed.Should().BeTrue();
	}

	[Fact]
	public void TryGetWithFailingFunction_WhenCalled_ShouldReturnFailureAndDefault()
	{
		(bool success, int value) = Try.TryGet<int>(() => throw new InvalidOperationException());

		success.Should().BeFalse();
		value.Should().Be(0);
	}

	[Fact]
	public void TryGetWithSuccessfulFunction_WhenCalled_ShouldReturnSuccessAndValue()
	{
		(bool success, int value) = Try.TryGet(() => 42);

		success.Should().BeTrue();
		value.Should().Be(42);
	}

	[Fact]
	public void TrySync_WhenCalled_ShouldDo_WithValidAction_ExecutesSuccessfully()
	{
		var executed = false;
		Try.Do(() => executed = true);
		executed.Should().BeTrue();
	}

	[Fact]
	public void TrySync_WhenCalled_ShouldGet_WithExceptionCallback_InvokesCallback()
	{
		Exception? capturedException = null;
		var action = () => Try.Get<int>(
			() => throw new InvalidOperationException("Test"),
			onException: ex => capturedException = ex
		);

		action.Should().Throw<InvalidOperationException>();
		capturedException.Should().NotBeNull();
		capturedException!.Message.Should().Be("Test");
	}

	[Fact]
	public void TrySync_WhenCalled_ShouldGetOrDefault_WithFactory_WithFailingFunction_CallsFactory()
	{
		int result = Try.GetOrDefault(() => throw new InvalidOperationException(), () => 99);

		result.Should().Be(99);
	}

	[Fact]
	public void TrySync_WhenCalled_ShouldShouldCatch_DoesNotCatchOutOfMemoryException()
	{
		var action = () => Try.TryDo(() => throw new OutOfMemoryException());

		action.Should().Throw<OutOfMemoryException>();
	}

	[Fact]
	public void TrySync_WhenCalled_ShouldShouldCatch_DoesNotCatchStackOverflowException()
	{
		var action = () => Try.TryDo(() => throw new StackOverflowException());

		action.Should().Throw<StackOverflowException>();
	}

	[Fact]
	public void TrySync_WhenCalled_ShouldTryDo_WithFailingAction_InvokesCallback()
	{
		Exception? capturedException = null;
		bool result = Try.TryDo(
			() => throw new InvalidOperationException("Test error"),
			ex => capturedException = ex
		);

		result.Should().BeFalse();
		capturedException.Should().NotBeNull();
		capturedException.Should().BeOfType<InvalidOperationException>();
		capturedException!.Message.Should().Be("Test error");
	}

	[Fact]
	public void TrySync_WhenCalled_ShouldTryGet_WithFailingFunction_InvokesCallback()
	{
		Exception? capturedException = null;
		(bool success, int _) = Try.TryGet<int>(
			() => throw new InvalidOperationException("Test error"),
			ex => capturedException = ex
		);

		success.Should().BeFalse();
		capturedException.Should().NotBeNull();
		capturedException!.Message.Should().Be("Test error");
	}
}
