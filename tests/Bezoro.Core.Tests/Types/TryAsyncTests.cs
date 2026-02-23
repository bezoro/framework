using System;
using System.Threading.Tasks;
using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(Try))]
public class TryAsyncTests
{
	[Fact]
	public async Task DoAsyncWithNullAction_WhenCalled_ShouldThrowArgumentNullException()
	{
		Func<Task>? nullAction = null;
		var         action     = () => Try.DoAsync(nullAction!);
		await action.Should().ThrowAsync<ArgumentNullException>();
	}

	[Fact]
	public async Task GetAsyncWithNullFunction_WhenCalled_ShouldThrowArgumentNullException()
	{
		Func<Task<int>>? nullFunc = null;
		var              action   = () => Try.GetAsync(nullFunc!);
		await action.Should().ThrowAsync<ArgumentNullException>();
	}

	[Fact]
	public async Task GetAsyncWithValidFunction_WhenCalled_ShouldReturnResult()
	{
		var func   = () => Task.FromResult(42);
		int result = await Try.GetAsync(func);
		result.Should().Be(42);
	}

	[Fact]
	public async Task GetOrDefaultAsyncWithFailingFunction_WhenCalled_ShouldReturnDefault()
	{
		int result = await Try.GetOrDefaultAsync(
						 () => Task.FromException<int>(new InvalidOperationException()),
						 99
					 );

		result.Should().Be(99);
	}

	[Fact]
	public async Task GetOrDefaultAsyncWithSuccessfulFunction_WhenCalled_ShouldReturnValue()
	{
		int result = await Try.GetOrDefaultAsync(() => Task.FromResult(42), 0);

		result.Should().Be(42);
	}

	[Fact]
	public async Task TryAsync_WhenCalled_ShouldDoAsync_WithValidAction_ExecutesSuccessfully()
	{
		var executed = false;
		var taskFunc = () =>
		{
			executed = true;
			return Task.CompletedTask;
		};

		await Try.DoAsync(taskFunc);

		executed.Should().BeTrue();
	}

	[Fact]
	public async Task TryAsync_WhenCalled_ShouldGetOrDefaultAsync_WithFactory_WithFailingFunction_CallsFactory()
	{
		int result = await Try.GetOrDefaultAsync(
						 () => Task.FromException<int>(new InvalidOperationException()),
						 () => 99
					 );

		result.Should().Be(99);
	}

	[Fact]
	public async Task TryDoAsyncWithFailingAction_WhenCalled_ShouldReturnFalse()
	{
		bool result = await Try.TryDoAsync(() => Task.FromException(new InvalidOperationException()));

		result.Should().BeFalse();
	}

	[Fact]
	public async Task TryDoAsyncWithSuccessfulAction_WhenCalled_ShouldReturnTrue()
	{
		var executed = false;
		bool result = await Try.TryDoAsync(() =>
						  {
							  executed = true;
							  return Task.CompletedTask;
						  }
					  );

		result.Should().BeTrue();
		executed.Should().BeTrue();
	}

	[Fact]
	public async Task TryGetAsyncWithFailingFunction_WhenCalled_ShouldReturnFailureAndDefault()
	{
		(bool success, int value) =
			await Try.TryGetAsync(() => Task.FromException<int>(new InvalidOperationException()));

		success.Should().BeFalse();
		value.Should().Be(0);
	}

	[Fact]
	public async Task TryGetAsyncWithSuccessfulFunction_WhenCalled_ShouldReturnSuccessAndValue()
	{
		(bool success, int value) = await Try.TryGetAsync(() => Task.FromResult(42));

		success.Should().BeTrue();
		value.Should().Be(42);
	}
}
