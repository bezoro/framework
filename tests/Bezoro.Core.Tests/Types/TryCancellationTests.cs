using System;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.Core.Types;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;

namespace Bezoro.Core.Tests.Types;

[TestSubject(typeof(Try))]
public class TryCancellationTests
{
	[Fact]
	public async Task DoAsyncWithCancellationTokenWhenCancelled_WhenCalled_ShouldThrowOperationCanceledException()
	{
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();
		var token = cts.Token;

		var action = () => Try.DoAsync(
			async ct => { await Task.Delay(100, ct); },
			cancellationToken: token
		);

		await action.Should().ThrowAsync<OperationCanceledException>();
	}

	[Fact]
	public async Task DoAsyncWithCancellationTokenWithNullAction_WhenCalled_ShouldThrowArgumentNullException()
	{
		Func<CancellationToken, Task>? nullAction = null;
		var                            action     = () => Try.DoAsync(nullAction!);
		await action.Should().ThrowAsync<ArgumentNullException>();
	}

	[Fact]
	public async Task GetAsyncWithCancellationTokenWhenCancelled_WhenCalled_ShouldThrowOperationCanceledException()
	{
		using var cts   = new CancellationTokenSource();
		var       token = cts.Token;

		await cts.CancelAsync();

		var action = () => Try.GetAsync(
			async ct =>
			{
				await Task.Delay(100, ct);
				return 42;
			},
			cancellationToken: token
		);

		await action.Should().ThrowAsync<OperationCanceledException>();
	}

	[Fact]
	public async Task GetAsyncWithCancellationTokenWithNullFunction_WhenCalled_ShouldThrowArgumentNullException()
	{
		Func<CancellationToken, Task<int>>? nullFunc = null;
		var                                 action   = () => Try.GetAsync(nullFunc!);
		await action.Should().ThrowAsync<ArgumentNullException>();
	}

	[Fact]
	public async Task GetAsyncWithCancellationTokenWithValidFunction_WhenCalled_ShouldReturnResult()
	{
		using var cts = new CancellationTokenSource();

		int result = await Try.GetAsync(
						 async ct =>
						 {
							 await Task.Delay(10, ct);
							 return 42;
						 },
						 cancellationToken: cts.Token
					 );

		result.Should().Be(42);
	}

	[Fact]
	public async Task
		GetOrDefaultAsync_WithCancellationToken_WithFactory_WithNullFactory_ThrowsArgumentNullException()
	{
		using var  cts         = new CancellationTokenSource();
		Func<int>? nullFactory = null;
		var        action      = () => Try.GetOrDefaultAsync(_ => Task.FromResult(42), nullFactory!);
		await action.Should().ThrowAsync<ArgumentNullException>();
	}

	[Fact]
	public async Task
		GetOrDefaultAsync_WithCancellationToken_WithFactory_WithNullFunction_ThrowsArgumentNullException()
	{
		Func<CancellationToken, Task<int>>? nullFunc = null;
		var                                 action   = () => Try.GetOrDefaultAsync(nullFunc!, () => 0);
		await action.Should().ThrowAsync<ArgumentNullException>();
	}

	[Fact]
	public async Task GetOrDefaultAsyncWithCancellationTokenWhenCancelled_WhenCalled_ShouldReturnDefault()
	{
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();

		int result = await Try.GetOrDefaultAsync(
						 async ct =>
						 {
							 await Task.Delay(100, ct);
							 return 42;
						 },
						 99,
						 cancellationToken: cts.Token
					 );

		result.Should().Be(99);
	}

	[Fact]
	public async Task
		GetOrDefaultAsyncWithCancellationTokenWithFactoryWithSuccessfulFunction_WhenCalled_ShouldReturnValue()
	{
		using var cts = new CancellationTokenSource();

		int result = await Try.GetOrDefaultAsync(
						 async ct =>
						 {
							 await Task.Delay(10, ct);
							 return 42;
						 },
						 () => 0,
						 cancellationToken: cts.Token
					 );

		result.Should().Be(42);
	}

	[Fact]
	public async Task GetOrDefaultAsyncWithCancellationTokenWithFailingFunction_WhenCalled_ShouldReturnDefault()
	{
		using var cts = new CancellationTokenSource();

		int result = await Try.GetOrDefaultAsync(
						 _ => Task.FromException<int>(new InvalidOperationException()),
						 99,
						 cancellationToken: cts.Token
					 );

		result.Should().Be(99);
	}

	[Fact]
	public async Task
		GetOrDefaultAsyncWithCancellationTokenWithNullFunction_WhenCalled_ShouldThrowArgumentNullException()
	{
		Func<CancellationToken, Task<int>>? nullFunc = null;
		var                                 action   = () => Try.GetOrDefaultAsync(nullFunc!, 0);
		await action.Should().ThrowAsync<ArgumentNullException>();
	}

	[Fact]
	public async Task GetOrDefaultAsyncWithCancellationTokenWithSuccessfulFunction_WhenCalled_ShouldReturnValue()
	{
		using var cts = new CancellationTokenSource();

		int result = await Try.GetOrDefaultAsync(
						 async ct =>
						 {
							 await Task.Delay(10, ct);
							 return 42;
						 },
						 0,
						 cancellationToken: cts.Token
					 );

		result.Should().Be(42);
	}

	[Fact]
	public async Task TryCancellation_WhenCalled_ShouldDoAsync_WithCancellationToken_WithException_InvokesCallback()
	{
		Exception? capturedException = null;
		using var  cts               = new CancellationTokenSource();
		var        token             = cts.Token;

		var action = () => Try.DoAsync(
			_ => Task.FromException(new InvalidOperationException("Test")),
			onException: ex => capturedException = ex,
			cancellationToken: token
		);

		await action.Should().ThrowAsync<InvalidOperationException>();
		capturedException.Should().NotBeNull();
		capturedException!.Message.Should().Be("Test");
	}

	[Fact]
	public async Task TryCancellation_WhenCalled_ShouldDoAsync_WithCancellationToken_WithException_TransformsException()
	{
		using var cts   = new CancellationTokenSource();
		var       token = cts.Token;

		var action = () => Try.DoAsync(
			_ => Task.FromException(new InvalidOperationException("Original")),
			ex => new ApplicationException("Transformed", ex),
			cancellationToken: token
		);

		var exception = await action.Should().ThrowAsync<ApplicationException>()
									.WithMessage("Transformed");

		exception.And.InnerException.Should().BeOfType<InvalidOperationException>();
	}

	[Fact]
	public async Task
		TryCancellation_WhenCalled_ShouldDoAsync_WithCancellationToken_WithValidAction_ExecutesSuccessfully()
	{
		var       executed = false;
		using var cts      = new CancellationTokenSource();

		await Try.DoAsync(
			async ct =>
			{
				await Task.Delay(10, ct);
				executed = true;
			},
			cancellationToken: cts.Token
		);

		executed.Should().BeTrue();
	}

	[Fact]
	public async Task TryCancellation_WhenCalled_ShouldGetAsync_WithCancellationToken_WithException_InvokesCallback()
	{
		Exception? capturedException = null;
		using var  cts               = new CancellationTokenSource();
		var        token             = cts.Token;

		var action = () => Try.GetAsync(
			_ => Task.FromException<int>(new InvalidOperationException("Test")),
			onException: ex => capturedException = ex,
			cancellationToken: token
		);

		await action.Should().ThrowAsync<InvalidOperationException>();
		capturedException.Should().NotBeNull();
		capturedException!.Message.Should().Be("Test");
	}

	[Fact]
	public async Task
		TryCancellation_WhenCalled_ShouldGetAsync_WithCancellationToken_WithException_TransformsException()
	{
		using var cts   = new CancellationTokenSource();
		var       token = cts.Token;

		var action = () => Try.GetAsync(
			_ => Task.FromException<int>(new InvalidOperationException("Original")),
			ex => new ApplicationException("Transformed", ex),
			cancellationToken: token
		);

		var exception = await action.Should().ThrowAsync<ApplicationException>()
									.WithMessage("Transformed");

		exception.And.InnerException.Should().BeOfType<InvalidOperationException>();
	}

	[Fact]
	public async Task
		TryCancellation_WhenCalled_ShouldGetOrDefaultAsync_WithCancellationToken_WithException_InvokesCallback()
	{
		Exception? capturedException = null;
		using var  cts               = new CancellationTokenSource();

		int result = await Try.GetOrDefaultAsync(
						 _ => Task.FromException<int>(new InvalidOperationException("Test")),
						 99,
						 ex => capturedException = ex,
						 cts.Token
					 );

		result.Should().Be(99);
		capturedException.Should().NotBeNull();
		capturedException!.Message.Should().Be("Test");
	}

	[Fact]
	public async Task
		TryCancellation_WhenCalled_ShouldGetOrDefaultAsync_WithCancellationToken_WithFactory_WhenCancelled_CallsFactory()
	{
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();

		int result = await Try.GetOrDefaultAsync(
						 async ct =>
						 {
							 await Task.Delay(100, ct);
							 return 42;
						 },
						 () => 99,
						 cancellationToken: cts.Token
					 );

		result.Should().Be(99);
	}

	[Fact]
	public async Task
		TryCancellation_WhenCalled_ShouldGetOrDefaultAsync_WithCancellationToken_WithFactory_WithFailingFunction_CallsFactory()
	{
		using var cts = new CancellationTokenSource();

		int result = await Try.GetOrDefaultAsync(
						 _ => Task.FromException<int>(new InvalidOperationException()),
						 () => 99,
						 cancellationToken: cts.Token
					 );

		result.Should().Be(99);
	}

	[Fact]
	public async Task TryCancellation_WhenCalled_ShouldTryDoAsync_WithCancellationToken_WithException_InvokesCallback()
	{
		Exception? capturedException = null;
		using var  cts               = new CancellationTokenSource();

		bool result = await Try.TryDoAsync(
						  _ => Task.FromException(new InvalidOperationException("Test")),
						  ex => capturedException = ex,
						  cts.Token
					  );

		result.Should().BeFalse();
		capturedException.Should().NotBeNull();
		capturedException!.Message.Should().Be("Test");
	}

	[Fact]
	public async Task TryCancellation_WhenCalled_ShouldTryGetAsync_WithCancellationToken_WithException_InvokesCallback()
	{
		Exception? capturedException = null;
		using var  cts               = new CancellationTokenSource();

		(bool success, int value) = await Try.TryGetAsync(
										_ => Task.FromException<int>(new InvalidOperationException("Test")),
										ex => capturedException = ex,
										cts.Token
									);

		success.Should().BeFalse();
		value.Should().Be(0);
		capturedException.Should().NotBeNull();
		capturedException!.Message.Should().Be("Test");
	}

	[Fact]
	public async Task TryDoAsyncWithCancellationTokenWhenCancelled_WhenCalled_ShouldReturnFalse()
	{
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();

		bool result = await Try.TryDoAsync(
						  async ct => { await Task.Delay(100, ct); },
						  cancellationToken: cts.Token
					  );

		result.Should().BeFalse();
	}

	[Fact]
	public async Task TryDoAsyncWithCancellationTokenWithFailingAction_WhenCalled_ShouldReturnFalse()
	{
		using var cts = new CancellationTokenSource();

		bool result = await Try.TryDoAsync(
						  _ => Task.FromException(new InvalidOperationException()),
						  cancellationToken: cts.Token
					  );

		result.Should().BeFalse();
	}

	[Fact]
	public async Task TryDoAsyncWithCancellationTokenWithNullAction_WhenCalled_ShouldThrowArgumentNullException()
	{
		Func<CancellationToken, Task>? nullAction = null;
		var                            action     = () => Try.TryDoAsync(nullAction!);
		await action.Should().ThrowAsync<ArgumentNullException>();
	}

	[Fact]
	public async Task TryDoAsyncWithCancellationTokenWithSuccessfulAction_WhenCalled_ShouldReturnTrue()
	{
		var       executed = false;
		using var cts      = new CancellationTokenSource();

		bool result = await Try.TryDoAsync(
						  async ct =>
						  {
							  await Task.Delay(10, ct);
							  executed = true;
						  },
						  cancellationToken: cts.Token
					  );

		result.Should().BeTrue();
		executed.Should().BeTrue();
	}

	[Fact]
	public async Task TryGetAsyncWithCancellationTokenWhenCancelled_WhenCalled_ShouldReturnFailureAndDefault()
	{
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();

		(bool success, int value) = await Try.TryGetAsync(
										async ct =>
										{
											await Task.Delay(100, ct);
											return 42;
										},
										cancellationToken: cts.Token
									);

		success.Should().BeFalse();
		value.Should().Be(0);
	}

	[Fact]
	public async Task TryGetAsyncWithCancellationTokenWithFailingFunction_WhenCalled_ShouldReturnFailureAndDefault()
	{
		using var cts = new CancellationTokenSource();

		(bool success, int value) = await Try.TryGetAsync(
										_ => Task.FromException<int>(new InvalidOperationException()),
										cancellationToken: cts.Token
									);

		success.Should().BeFalse();
		value.Should().Be(0);
	}

	[Fact]
	public async Task TryGetAsyncWithCancellationTokenWithNullFunction_WhenCalled_ShouldThrowArgumentNullException()
	{
		Func<CancellationToken, Task<int>>? nullFunc = null;
		var                                 action   = () => Try.TryGetAsync(nullFunc!);
		await action.Should().ThrowAsync<ArgumentNullException>();
	}

	[Fact]
	public async Task TryGetAsyncWithCancellationTokenWithSuccessfulFunction_WhenCalled_ShouldReturnSuccessAndValue()
	{
		using var cts = new CancellationTokenSource();

		(bool success, int value) = await Try.TryGetAsync(
										async ct =>
										{
											await Task.Delay(10, ct);
											return 42;
										},
										cancellationToken: cts.Token
									);

		success.Should().BeTrue();
		value.Should().Be(42);
	}
}
