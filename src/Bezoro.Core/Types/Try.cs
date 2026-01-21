using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Bezoro.Core.Extensions;

namespace Bezoro.Core.Types;

/// <summary>
///     <para>
///         Provides utility methods for safe execution of functions and actions with exception handling.
///         Supports both result-based patterns (Try*) and exception-throwing patterns (Do/Get).
///     </para>
///     <example>
///         <code>
///     // Result-based (no exceptions thrown)
///     var (success, value) = Try.TryGet(() => ParseData());
///     if (success) { /* use value */ }
/// 
///     // Exception-based (throws on error)
///     var result = Try.Get(() => ParseData());
/// 
///     // With fallback value
///     var result = Try.GetOrDefault(() => ParseData(), defaultValue);
/// 
///     // With exception transformation
///     var result = Try.Get(() => ParseData(), ex => new DataException("Parse failed", ex));
/// 
///     // With logging callback
///     var result = Try.Get(() => ParseData(), onException: ex => _logger.LogError(ex, "Parse failed"));
/// 
///     // Async with cancellation
///     var result = await Try.GetAsync(ct => FetchDataAsync(ct), cancellationToken: cts.Token);
///     </code>
///     </example>
/// </summary>
public static class Try
{
	/// <summary>
	///     Executes a function safely and returns whether it succeeded along with the result.
	/// </summary>
	/// <typeparam name="T">The type of the return value.</typeparam>
	/// <param name="func">The function to execute.</param>
	/// <param name="onException">Optional callback invoked when an exception occurs.</param>
	/// <returns>A tuple containing success status and the result (default if failed).</returns>
	/// <exception cref="ArgumentNullException">Thrown when func is null.</exception>
	public static (bool Success, T? Value) TryGet<T>(
		Func<T?>           func,
		Action<Exception>? onException = null
	)
	{
		func.ThrowIfNull();

		try
		{
			return (true, func());
		}
		catch (Exception ex) when (ShouldCatch(ex))
		{
			onException?.Invoke(ex);
			return (false, default);
		}
	}

	/// <summary>
	///     Executes an action safely and returns whether it succeeded.
	/// </summary>
	/// <param name="action">The action to execute.</param>
	/// <param name="onException">Optional callback invoked when an exception occurs.</param>
	/// <returns>True if the action executed successfully; otherwise, false.</returns>
	/// <exception cref="ArgumentNullException">Thrown when action is null.</exception>
	public static bool TryDo(
		Action             action,
		Action<Exception>? onException = null
	)
	{
		action.ThrowIfNull();

		try
		{
			action();
			return true;
		}
		catch (Exception ex) when (ShouldCatch(ex))
		{
			onException?.Invoke(ex);
			return false;
		}
	}

	/// <summary>
	///     Executes a function safely and returns its result, or a default value if it fails.
	/// </summary>
	/// <typeparam name="T">The type of the return value.</typeparam>
	/// <param name="func">The function to execute.</param>
	/// <param name="defaultValue">The value to return if the function fails.</param>
	/// <param name="onException">Optional callback invoked when an exception occurs.</param>
	/// <returns>The result of the function execution, or defaultValue if it fails.</returns>
	/// <exception cref="ArgumentNullException">Thrown when func is null.</exception>
	public static T GetOrDefault<T>(
		Func<T>            func,
		T                  defaultValue,
		Action<Exception>? onException = null
	)
	{
		func.ThrowIfNull();

		try
		{
			return func();
		}
		catch (Exception ex) when (ShouldCatch(ex))
		{
			onException?.Invoke(ex);
			return defaultValue;
		}
	}

	/// <summary>
	///     Executes a function safely and returns its result, or a default value from a factory if it fails.
	/// </summary>
	/// <typeparam name="T">The type of the return value.</typeparam>
	/// <param name="func">The function to execute.</param>
	/// <param name="defaultValueFactory">A function that produces the default value if the main function fails.</param>
	/// <param name="onException">Optional callback invoked when an exception occurs.</param>
	/// <returns>The result of the function execution, or the result of defaultValueFactory if it fails.</returns>
	/// <exception cref="ArgumentNullException">Thrown when func or defaultValueFactory is null.</exception>
	public static T GetOrDefault<T>(
		Func<T>            func,
		Func<T>            defaultValueFactory,
		Action<Exception>? onException = null
	)
	{
		func.ThrowIfNull();
		defaultValueFactory.ThrowIfNull();

		try
		{
			return func();
		}
		catch (Exception ex) when (ShouldCatch(ex))
		{
			onException?.Invoke(ex);
			return defaultValueFactory();
		}
	}

	/// <summary>
	///     Executes a function safely and returns its result, transforming exceptions if specified.
	/// </summary>
	/// <typeparam name="T">The type of the return value.</typeparam>
	/// <param name="func">The function to execute.</param>
	/// <param name="exceptionTransform">Optional function to transform the exception before throwing.</param>
	/// <param name="onException">Optional callback invoked when an exception occurs (before transformation).</param>
	/// <returns>The result of the function execution.</returns>
	/// <exception cref="ArgumentNullException">Thrown when func is null.</exception>
	public static T? Get<T>(
		Func<T?>                    func,
		Func<Exception, Exception>? exceptionTransform = null,
		Action<Exception>?          onException        = null
	)
	{
		func.ThrowIfNull();

		try
		{
			return func();
		}
		catch (Exception ex) when (ShouldCatch(ex))
		{
			onException?.Invoke(ex);
			ThrowTransformed(ex, exceptionTransform);
			throw; // Unreachable, but satisfies compiler
		}
	}

	/// <summary>
	///     Executes an asynchronous action safely, transforming exceptions if specified.
	/// </summary>
	/// <param name="action">The async action to execute (receives cancellation token).</param>
	/// <param name="exceptionTransform">Optional function to transform the exception before throwing.</param>
	/// <param name="onException">Optional callback invoked when an exception occurs (before transformation).</param>
	/// <param name="cancellationToken">Optional cancellation token passed to the action.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	/// <exception cref="ArgumentNullException">Thrown when action is null.</exception>
	/// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
	public static async Task DoAsync(
		Func<CancellationToken, Task> action,
		Func<Exception, Exception>?   exceptionTransform = null,
		Action<Exception>?            onException        = null,
		CancellationToken             cancellationToken  = default
	)
	{
		action.ThrowIfNull();

		try
		{
			await action(cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex) when (ShouldCatch(ex))
		{
			onException?.Invoke(ex);
			ThrowTransformed(ex, exceptionTransform);
		}
	}

	/// <summary>
	///     Executes an asynchronous action safely, transforming exceptions if specified.
	///     Overload for actions that don't require cancellation support.
	/// </summary>
	/// <param name="action">The async action to execute.</param>
	/// <param name="exceptionTransform">Optional function to transform the exception before throwing.</param>
	/// <param name="onException">Optional callback invoked when an exception occurs (before transformation).</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	/// <exception cref="ArgumentNullException">Thrown when action is null.</exception>
	public static async Task DoAsync(
		Func<Task>                  action,
		Func<Exception, Exception>? exceptionTransform = null,
		Action<Exception>?          onException        = null
	)
	{
		action.ThrowIfNull();

		try
		{
			await action().ConfigureAwait(false);
		}
		catch (Exception ex) when (ShouldCatch(ex))
		{
			onException?.Invoke(ex);
			ThrowTransformed(ex, exceptionTransform);
		}
	}

	/// <summary>
	///     Executes an asynchronous function safely and returns whether it succeeded along with the result.
	/// </summary>
	/// <typeparam name="T">The type of the return value.</typeparam>
	/// <param name="func">The async function to execute (receives cancellation token).</param>
	/// <param name="onException">Optional callback invoked when an exception occurs.</param>
	/// <param name="cancellationToken">Optional cancellation token passed to the function.</param>
	/// <returns>A task representing the asynchronous operation with success status and result.</returns>
	/// <exception cref="ArgumentNullException">Thrown when func is null.</exception>
	/// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
	public static async Task<(bool Success, T? Value)> TryGetAsync<T>(
		Func<CancellationToken, Task<T?>> func,
		Action<Exception>?                onException       = null,
		CancellationToken                 cancellationToken = default
	)
	{
		func.ThrowIfNull();

		try
		{
			var result = await func(cancellationToken).ConfigureAwait(false);
			return (true, result);
		}
		catch (Exception ex) when (ShouldCatch(ex))
		{
			onException?.Invoke(ex);
			return (false, default);
		}
	}

	/// <summary>
	///     Executes an asynchronous function safely and returns whether it succeeded along with the result.
	///     Overload for functions that don't require cancellation support.
	/// </summary>
	/// <typeparam name="T">The type of the return value.</typeparam>
	/// <param name="func">The async function to execute.</param>
	/// <param name="onException">Optional callback invoked when an exception occurs.</param>
	/// <returns>A task representing the asynchronous operation with success status and result.</returns>
	/// <exception cref="ArgumentNullException">Thrown when func is null.</exception>
	public static async Task<(bool Success, T? Value)> TryGetAsync<T>(
		Func<Task<T?>>     func,
		Action<Exception>? onException = null
	)
	{
		func.ThrowIfNull();

		try
		{
			var result = await func().ConfigureAwait(false);
			return (true, result);
		}
		catch (Exception ex) when (ShouldCatch(ex))
		{
			onException?.Invoke(ex);
			return (false, default);
		}
	}

	/// <summary>
	///     Executes an asynchronous action safely and returns whether it succeeded.
	/// </summary>
	/// <param name="action">The async action to execute (receives cancellation token).</param>
	/// <param name="onException">Optional callback invoked when an exception occurs.</param>
	/// <param name="cancellationToken">Optional cancellation token passed to the action.</param>
	/// <returns>A task representing the asynchronous operation with success status.</returns>
	/// <exception cref="ArgumentNullException">Thrown when action is null.</exception>
	/// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
	public static async Task<bool> TryDoAsync(
		Func<CancellationToken, Task> action,
		Action<Exception>?            onException       = null,
		CancellationToken             cancellationToken = default
	)
	{
		action.ThrowIfNull();

		try
		{
			await action(cancellationToken).ConfigureAwait(false);
			return true;
		}
		catch (Exception ex) when (ShouldCatch(ex))
		{
			onException?.Invoke(ex);
			return false;
		}
	}

	/// <summary>
	///     Executes an asynchronous action safely and returns whether it succeeded.
	///     Overload for actions that don't require cancellation support.
	/// </summary>
	/// <param name="action">The async action to execute.</param>
	/// <param name="onException">Optional callback invoked when an exception occurs.</param>
	/// <returns>A task representing the asynchronous operation with success status.</returns>
	/// <exception cref="ArgumentNullException">Thrown when action is null.</exception>
	public static async Task<bool> TryDoAsync(
		Func<Task>         action,
		Action<Exception>? onException = null
	)
	{
		action.ThrowIfNull();

		try
		{
			await action().ConfigureAwait(false);
			return true;
		}
		catch (Exception ex) when (ShouldCatch(ex))
		{
			onException?.Invoke(ex);
			return false;
		}
	}

	/// <summary>
	///     Executes an asynchronous function safely and returns its result, transforming exceptions if specified.
	/// </summary>
	/// <typeparam name="T">The type of the return value.</typeparam>
	/// <param name="func">The async function to execute (receives cancellation token).</param>
	/// <param name="exceptionTransform">Optional function to transform the exception before throwing.</param>
	/// <param name="onException">Optional callback invoked when an exception occurs (before transformation).</param>
	/// <param name="cancellationToken">Optional cancellation token passed to the function.</param>
	/// <returns>A task representing the asynchronous operation with the function result.</returns>
	/// <exception cref="ArgumentNullException">Thrown when func is null.</exception>
	/// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
	public static async Task<T?> GetAsync<T>(
		Func<CancellationToken, Task<T?>> func,
		Func<Exception, Exception>?       exceptionTransform = null,
		Action<Exception>?                onException        = null,
		CancellationToken                 cancellationToken  = default
	)
	{
		func.ThrowIfNull();

		try
		{
			return await func(cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex) when (ShouldCatch(ex))
		{
			onException?.Invoke(ex);
			ThrowTransformed(ex, exceptionTransform);
			throw; // Unreachable, but satisfies compiler
		}
	}

	/// <summary>
	///     Executes an asynchronous function safely and returns its result, transforming exceptions if specified.
	///     Overload for functions that don't require cancellation support.
	/// </summary>
	/// <typeparam name="T">The type of the return value.</typeparam>
	/// <param name="func">The async function to execute.</param>
	/// <param name="exceptionTransform">Optional function to transform the exception before throwing.</param>
	/// <param name="onException">Optional callback invoked when an exception occurs (before transformation).</param>
	/// <returns>A task representing the asynchronous operation with the function result.</returns>
	/// <exception cref="ArgumentNullException">Thrown when func is null.</exception>
	public static async Task<T?> GetAsync<T>(
		Func<Task<T?>>              func,
		Func<Exception, Exception>? exceptionTransform = null,
		Action<Exception>?          onException        = null
	)
	{
		func.ThrowIfNull();

		try
		{
			return await func().ConfigureAwait(false);
		}
		catch (Exception ex) when (ShouldCatch(ex))
		{
			onException?.Invoke(ex);
			ThrowTransformed(ex, exceptionTransform);
			throw; // Unreachable, but satisfies compiler
		}
	}

	/// <summary>
	///     Executes an asynchronous function safely and returns its result, or a default value if it fails.
	/// </summary>
	/// <typeparam name="T">The type of the return value.</typeparam>
	/// <param name="func">The async function to execute (receives cancellation token).</param>
	/// <param name="defaultValue">The value to return if the function fails.</param>
	/// <param name="onException">Optional callback invoked when an exception occurs.</param>
	/// <param name="cancellationToken">Optional cancellation token passed to the function.</param>
	/// <returns>A task representing the asynchronous operation with the function result or defaultValue.</returns>
	/// <exception cref="ArgumentNullException">Thrown when func is null.</exception>
	/// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
	public static async Task<T> GetOrDefaultAsync<T>(
		Func<CancellationToken, Task<T>> func,
		T                                defaultValue,
		Action<Exception>?               onException       = null,
		CancellationToken                cancellationToken = default
	)
	{
		func.ThrowIfNull();

		try
		{
			return await func(cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex) when (ShouldCatch(ex))
		{
			onException?.Invoke(ex);
			return defaultValue;
		}
	}

	/// <summary>
	///     Executes an asynchronous function safely and returns its result, or a default value if it fails.
	///     Overload for functions that don't require cancellation support.
	/// </summary>
	/// <typeparam name="T">The type of the return value.</typeparam>
	/// <param name="func">The async function to execute.</param>
	/// <param name="defaultValue">The value to return if the function fails.</param>
	/// <param name="onException">Optional callback invoked when an exception occurs.</param>
	/// <returns>A task representing the asynchronous operation with the function result or defaultValue.</returns>
	/// <exception cref="ArgumentNullException">Thrown when func is null.</exception>
	public static async Task<T> GetOrDefaultAsync<T>(
		Func<Task<T>>      func,
		T                  defaultValue,
		Action<Exception>? onException = null
	)
	{
		func.ThrowIfNull();

		try
		{
			return await func().ConfigureAwait(false);
		}
		catch (Exception ex) when (ShouldCatch(ex))
		{
			onException?.Invoke(ex);
			return defaultValue;
		}
	}

	/// <summary>
	///     Executes an asynchronous function safely and returns its result, or a default value from a factory if it fails.
	/// </summary>
	/// <typeparam name="T">The type of the return value.</typeparam>
	/// <param name="func">The async function to execute (receives cancellation token).</param>
	/// <param name="defaultValueFactory">A function that produces the default value if the main function fails.</param>
	/// <param name="onException">Optional callback invoked when an exception occurs.</param>
	/// <param name="cancellationToken">Optional cancellation token passed to the function.</param>
	/// <returns>A task representing the asynchronous operation with the function result or the result of defaultValueFactory.</returns>
	/// <exception cref="ArgumentNullException">Thrown when func or defaultValueFactory is null.</exception>
	/// <exception cref="OperationCanceledException">Thrown when the operation is cancelled.</exception>
	public static async Task<T> GetOrDefaultAsync<T>(
		Func<CancellationToken, Task<T>> func,
		Func<T>                          defaultValueFactory,
		Action<Exception>?               onException       = null,
		CancellationToken                cancellationToken = default
	)
	{
		func.ThrowIfNull();
		defaultValueFactory.ThrowIfNull();

		try
		{
			return await func(cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex) when (ShouldCatch(ex))
		{
			onException?.Invoke(ex);
			return defaultValueFactory();
		}
	}

	/// <summary>
	///     Executes an asynchronous function safely and returns its result, or a default value from a factory if it fails.
	///     Overload for functions that don't require cancellation support.
	/// </summary>
	/// <typeparam name="T">The type of the return value.</typeparam>
	/// <param name="func">The async function to execute.</param>
	/// <param name="defaultValueFactory">A function that produces the default value if the main function fails.</param>
	/// <param name="onException">Optional callback invoked when an exception occurs.</param>
	/// <returns>A task representing the asynchronous operation with the function result or the result of defaultValueFactory.</returns>
	/// <exception cref="ArgumentNullException">Thrown when func or defaultValueFactory is null.</exception>
	public static async Task<T> GetOrDefaultAsync<T>(
		Func<Task<T>>      func,
		Func<T>            defaultValueFactory,
		Action<Exception>? onException = null
	)
	{
		func.ThrowIfNull();
		defaultValueFactory.ThrowIfNull();

		try
		{
			return await func().ConfigureAwait(false);
		}
		catch (Exception ex) when (ShouldCatch(ex))
		{
			onException?.Invoke(ex);
			return defaultValueFactory();
		}
	}

	/// <summary>
	///     Executes an action safely, transforming exceptions if specified.
	/// </summary>
	/// <param name="action">The action to execute.</param>
	/// <param name="exceptionTransform">Optional function to transform the exception before throwing.</param>
	/// <param name="onException">Optional callback invoked when an exception occurs (before transformation).</param>
	/// <exception cref="ArgumentNullException">Thrown when action is null.</exception>
	public static void Do(
		Action                      action,
		Func<Exception, Exception>? exceptionTransform = null,
		Action<Exception>?          onException        = null
	)
	{
		action.ThrowIfNull();

		try
		{
			action();
		}
		catch (Exception ex) when (ShouldCatch(ex))
		{
			onException?.Invoke(ex);
			ThrowTransformed(ex, exceptionTransform);
		}
	}

	/// <summary>
	///     Determines whether an exception should be caught or allowed to propagate.
	///     Critical exceptions like OutOfMemoryException and StackOverflowException are never caught.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool ShouldCatch(Exception ex) =>
		// Let critical exceptions propagate
		ex is not (OutOfMemoryException or StackOverflowException);

	/// <summary>
	///     Throws an exception, optionally transforming it first.
	/// </summary>
	[DoesNotReturn]
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void ThrowTransformed(Exception original, Func<Exception, Exception>? transform)
	{
		if (transform != null) throw transform(original);

		throw original;
	}
}
