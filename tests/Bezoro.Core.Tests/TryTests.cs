using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using JetBrains.Annotations;
using Xunit;
using Bezoro.Core.Types;

namespace Bezoro.Core.Tests;

[TestSubject(typeof(Try))]
public static class TryTests
{
	public class Unit
	{
		[Fact]
		public async Task DoAsync_WithCancellationToken_WhenCancelled_ThrowsOperationCanceledException()
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
		public async Task DoAsync_WithCancellationToken_WithException_InvokesCallback()
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
		public async Task DoAsync_WithCancellationToken_WithException_TransformsException()
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
		public async Task DoAsync_WithCancellationToken_WithNullAction_ThrowsArgumentNullException()
		{
			Func<CancellationToken, Task>? nullAction = null;
			var                            action     = () => Try.DoAsync(nullAction!);
			await action.Should().ThrowAsync<ArgumentNullException>();
		}

		[Fact]
		public async Task DoAsync_WithCancellationToken_WithValidAction_ExecutesSuccessfully()
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
		public async Task DoAsync_WithNullAction_ThrowsArgumentNullException()
		{
			Func<Task>? nullAction = null;
			var         action     = () => Try.DoAsync(nullAction!);
			await action.Should().ThrowAsync<ArgumentNullException>();
		}

		[Fact]
		public async Task DoAsync_WithValidAction_ExecutesSuccessfully()
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
		public async Task GetAsync_WithCancellationToken_WhenCancelled_ThrowsOperationCanceledException()
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
		public async Task GetAsync_WithCancellationToken_WithException_InvokesCallback()
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
		public async Task GetAsync_WithCancellationToken_WithException_TransformsException()
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
		public async Task GetAsync_WithCancellationToken_WithNullFunction_ThrowsArgumentNullException()
		{
			Func<CancellationToken, Task<int>>? nullFunc = null;
			var                                 action   = () => Try.GetAsync(nullFunc!);
			await action.Should().ThrowAsync<ArgumentNullException>();
		}

		[Fact]
		public async Task GetAsync_WithCancellationToken_WithValidFunction_ReturnsResult()
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
		public async Task GetAsync_WithNullFunction_ThrowsArgumentNullException()
		{
			Func<Task<int>>? nullFunc = null;
			var              action   = () => Try.GetAsync(nullFunc!);
			await action.Should().ThrowAsync<ArgumentNullException>();
		}

		[Fact]
		public async Task GetAsync_WithValidFunction_ReturnsResult()
		{
			var func   = () => Task.FromResult(42);
			int result = await Try.GetAsync(func);
			result.Should().Be(42);
		}

		[Fact]
		public async Task GetOrDefaultAsync_WithCancellationToken_WhenCancelled_ReturnsDefault()
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
		public async Task GetOrDefaultAsync_WithCancellationToken_WithException_InvokesCallback()
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
		public async Task GetOrDefaultAsync_WithCancellationToken_WithFactory_WhenCancelled_CallsFactory()
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
		public async Task GetOrDefaultAsync_WithCancellationToken_WithFactory_WithFailingFunction_CallsFactory()
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
		public async Task GetOrDefaultAsync_WithCancellationToken_WithFactory_WithSuccessfulFunction_ReturnsValue()
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
		public async Task GetOrDefaultAsync_WithCancellationToken_WithFailingFunction_ReturnsDefault()
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
		public async Task GetOrDefaultAsync_WithCancellationToken_WithNullFunction_ThrowsArgumentNullException()
		{
			Func<CancellationToken, Task<int>>? nullFunc = null;
			var                                 action   = () => Try.GetOrDefaultAsync(nullFunc!, 0);
			await action.Should().ThrowAsync<ArgumentNullException>();
		}

		[Fact]
		public async Task GetOrDefaultAsync_WithCancellationToken_WithSuccessfulFunction_ReturnsValue()
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
		public async Task GetOrDefaultAsync_WithFactory_WithFailingFunction_CallsFactory()
		{
			int result = await Try.GetOrDefaultAsync(
							 () => Task.FromException<int>(new InvalidOperationException()),
							 () => 99
						 );

			result.Should().Be(99);
		}

		[Fact]
		public async Task GetOrDefaultAsync_WithFailingFunction_ReturnsDefault()
		{
			int result = await Try.GetOrDefaultAsync(
							 () => Task.FromException<int>(new InvalidOperationException()),
							 99);

			result.Should().Be(99);
		}

		[Fact]
		public async Task GetOrDefaultAsync_WithSuccessfulFunction_ReturnsValue()
		{
			int result = await Try.GetOrDefaultAsync(() => Task.FromResult(42), 0);

			result.Should().Be(42);
		}

		[Fact]
		public async Task TryDoAsync_WithCancellationToken_WhenCancelled_ReturnsFalse()
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
		public async Task TryDoAsync_WithCancellationToken_WithException_InvokesCallback()
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
		public async Task TryDoAsync_WithCancellationToken_WithFailingAction_ReturnsFalse()
		{
			using var cts = new CancellationTokenSource();

			bool result = await Try.TryDoAsync(
							  _ => Task.FromException(new InvalidOperationException()),
							  cancellationToken: cts.Token
						  );

			result.Should().BeFalse();
		}

		[Fact]
		public async Task TryDoAsync_WithCancellationToken_WithNullAction_ThrowsArgumentNullException()
		{
			Func<CancellationToken, Task>? nullAction = null;
			var                            action     = () => Try.TryDoAsync(nullAction!);
			await action.Should().ThrowAsync<ArgumentNullException>();
		}

		[Fact]
		public async Task TryDoAsync_WithCancellationToken_WithSuccessfulAction_ReturnsTrue()
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
		public async Task TryDoAsync_WithFailingAction_ReturnsFalse()
		{
			bool result = await Try.TryDoAsync(() => Task.FromException(new InvalidOperationException()));

			result.Should().BeFalse();
		}

		[Fact]
		public async Task TryDoAsync_WithSuccessfulAction_ReturnsTrue()
		{
			var executed = false;
			bool result = await Try.TryDoAsync(() =>
			{
				executed = true;
				return Task.CompletedTask;
			});

			result.Should().BeTrue();
			executed.Should().BeTrue();
		}

		[Fact]
		public async Task TryGetAsync_WithCancellationToken_WhenCancelled_ReturnsFailureAndDefault()
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
		public async Task TryGetAsync_WithCancellationToken_WithException_InvokesCallback()
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
		public async Task TryGetAsync_WithCancellationToken_WithFailingFunction_ReturnsFailureAndDefault()
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
		public async Task TryGetAsync_WithCancellationToken_WithNullFunction_ThrowsArgumentNullException()
		{
			Func<CancellationToken, Task<int>>? nullFunc = null;
			var                                 action   = () => Try.TryGetAsync(nullFunc!);
			await action.Should().ThrowAsync<ArgumentNullException>();
		}

		[Fact]
		public async Task TryGetAsync_WithCancellationToken_WithSuccessfulFunction_ReturnsSuccessAndValue()
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

		[Fact]
		public async Task TryGetAsync_WithFailingFunction_ReturnsFailureAndDefault()
		{
			(bool success, int value) =
				await Try.TryGetAsync(() => Task.FromException<int>(new InvalidOperationException()));

			success.Should().BeFalse();
			value.Should().Be(0);
		}

		[Fact]
		public async Task TryGetAsync_WithSuccessfulFunction_ReturnsSuccessAndValue()
		{
			(bool success, int value) = await Try.TryGetAsync(() => Task.FromResult(42));

			success.Should().BeTrue();
			value.Should().Be(42);
		}

		[Fact]
		public void Do_WithNullAction_ThrowsArgumentNullException()
		{
			var action = () => Try.Do(null!);
			action.Should().Throw<ArgumentNullException>();
		}

		[Fact]
		public void Do_WithValidAction_ExecutesSuccessfully()
		{
			var executed = false;
			Try.Do(() => executed = true);
			executed.Should().BeTrue();
		}

		[Fact]
		public void Get_WithExceptionCallback_InvokesCallback()
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
		public void Get_WithExceptionTransform_ThrowsTransformedException()
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
		public void Get_WithNullFunction_ThrowsArgumentNullException()
		{
			var action = () => Try.Get<int>(null!);
			action.Should().Throw<ArgumentNullException>();
		}

		[Fact]
		public void Get_WithValidFunction_ReturnsResult()
		{
			int result = Try.Get(() => 42);
			result.Should().Be(42);
		}

		[Fact]
		public void GetOrDefault_WithFactory_WithFailingFunction_CallsFactory()
		{
			int result = Try.GetOrDefault(() => throw new InvalidOperationException(), () => 99);

			result.Should().Be(99);
		}

		[Fact]
		public void GetOrDefault_WithFactory_WithSuccessfulFunction_ReturnsValue()
		{
			int result = Try.GetOrDefault(() => 42, () => 0);

			result.Should().Be(42);
		}

		[Fact]
		public void GetOrDefault_WithFailingFunction_ReturnsDefault()
		{
			int result = Try.GetOrDefault(() => throw new InvalidOperationException(), 99);

			result.Should().Be(99);
		}

		[Fact]
		public void GetOrDefault_WithSuccessfulFunction_ReturnsValue()
		{
			int result = Try.GetOrDefault(() => 42, 0);

			result.Should().Be(42);
		}

		[Fact]
		public void ShouldCatch_DoesNotCatchOutOfMemoryException()
		{
			var action = () => Try.TryDo(() => throw new OutOfMemoryException());

			action.Should().Throw<OutOfMemoryException>();
		}

		[Fact]
		public void ShouldCatch_DoesNotCatchStackOverflowException()
		{
			var action = () => Try.TryDo(() => throw new StackOverflowException());

			action.Should().Throw<StackOverflowException>();
		}

		[Fact]
		public void TryDo_WithFailingAction_InvokesCallback()
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
		public void TryDo_WithFailingAction_ReturnsFalse()
		{
			bool result = Try.TryDo(() => throw new InvalidOperationException());

			result.Should().BeFalse();
		}

		[Fact]
		public void TryDo_WithSuccessfulAction_ReturnsTrue()
		{
			var  executed = false;
			bool result   = Try.TryDo(() => executed = true);

			result.Should().BeTrue();
			executed.Should().BeTrue();
		}

		[Fact]
		public void TryGet_WithFailingFunction_InvokesCallback()
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

		[Fact]
		public void TryGet_WithFailingFunction_ReturnsFailureAndDefault()
		{
			(bool success, int value) = Try.TryGet<int>(() => throw new InvalidOperationException());

			success.Should().BeFalse();
			value.Should().Be(0);
		}

		[Fact]
		public void TryGet_WithSuccessfulFunction_ReturnsSuccessAndValue()
		{
			(bool success, int value) = Try.TryGet(() => 42);

			success.Should().BeTrue();
			value.Should().Be(42);
		}
	}
}
