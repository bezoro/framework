using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Bezoro.Core.Common.Extensions;

namespace Bezoro.Core;

/// <summary>
///     Provides utility methods for safe execution of functions and actions with exception handling.
/// </summary>
public static class Validate
{
	/// <summary>
	///     Executes a function safely and returns its result.
	/// </summary>
	/// <typeparam name="T">The type of the return value.</typeparam>
	/// <param name="func">The function to execute.</param>
	/// <param name="alternativeException">Custom exception to throw instead of the original one.</param>
	/// <param name="errorMessage">Custom error message to wrap the original exception.</param>
	/// <returns>The result of the function execution.</returns>
	/// <exception cref="ArgumentNullException">Thrown when func is null.</exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static T? Get<T>(
		Func<T?>   func,
		Exception? alternativeException = null,
		string?    errorMessage         = null
	) =>
		InternalGet(func, alternativeException, errorMessage);

	/// <summary>
	///     Executes an asynchronous action safely.
	/// </summary>
	/// <param name="action">The async action to execute.</param>
	/// <param name="custom">Custom exception to throw instead of the original one.</param>
	/// <param name="message">Custom error message to wrap the original exception.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	/// <exception cref="ArgumentNullException">Thrown when action is null.</exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Task DoAsync(
		Func<Task> action,
		Exception? custom  = null,
		string?    message = null
	) =>
		InternalDoAsync(action, custom, message);

	/// <summary>
	///     Executes an asynchronous function safely and returns its result.
	/// </summary>
	/// <typeparam name="T">The type of the return value.</typeparam>
	/// <param name="func">The async function to execute.</param>
	/// <param name="custom">Custom exception to throw instead of the original one.</param>
	/// <param name="message">Custom error message to wrap the original exception.</param>
	/// <returns>A task representing the asynchronous operation with the function result.</returns>
	/// <exception cref="ArgumentNullException">Thrown when func is null.</exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Task<T?> GetAsync<T>(
		Func<Task<T?>> func,
		Exception?     custom  = null,
		string?        message = null
	) =>
		InternalGetAsync(func, custom, message);

	/// <summary>
	///     Executes an action safely.
	/// </summary>
	/// <param name="action">The action to execute.</param>
	/// <param name="custom">Custom exception to throw instead of the original one.</param>
	/// <param name="message">Custom error message to wrap the original exception.</param>
	/// <exception cref="ArgumentNullException">Thrown when action is null.</exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void Do(
		Action     action,
		Exception? custom  = null,
		string?    message = null
	) =>
		InternalDo(action, custom, message);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static T? InternalGet<T>(Func<T?> func, Exception? custom, string? msg)
	{
		func.ThrowIfNull();

		try
		{
			return func();
		}
		catch (Exception e)
		{
			Throw(custom, msg, e);
		}

		return default;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static async Task InternalDoAsync(
		Func<Task> func,
		Exception? custom,
		string?    msg
	)
	{
		func.ThrowIfNull();

		try
		{
			await func().ConfigureAwait(false);
		}
		catch (Exception e)
		{
			Throw(custom, msg, e);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static async Task<T?> InternalGetAsync<T>(
		Func<Task<T?>> func,
		Exception?     custom,
		string?        msg
	)
	{
		func.ThrowIfNull();

		try
		{
			return await func().ConfigureAwait(false);
		}
		catch (Exception e)
		{
			Throw(custom, msg, e);
		}

		return default;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void InternalDo(
		Action     action,
		Exception? custom,
		string?    msg
	)
	{
		action.ThrowIfNull();

		try
		{
			action();
		}
		catch (Exception e)
		{
			Throw(custom, msg, e);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void Throw(Exception? custom, string? msg, Exception original)
	{
		if (custom != null) throw custom;

		if (!string.IsNullOrEmpty(msg)) throw new(msg, original);

		throw original;
	}
}
