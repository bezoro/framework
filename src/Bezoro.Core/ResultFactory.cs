using System;
using System.Runtime.CompilerServices;
using Bezoro.Core.Common.Extensions;

namespace Bezoro.Core;

/// <summary>
///     Provides static factory methods for creating Result instances.
/// </summary>
public static class ResultFactory
{
	/// <summary>
	///     Creates a failed result with the specified reason.
	/// </summary>
	/// <typeparam name="T">The type of the result data.</typeparam>
	/// <param name="reason">The reason for the failure.</param>
	/// <returns>A new failed Result instance.</returns>
	public static Result<T> Failed<T>(IFailureReason reason) where T : notnull => Result<T>.Failed(reason);

	/// <summary>
	///     Creates a successful result with the specified data.
	/// </summary>
	/// <typeparam name="T">The type of the result data.</typeparam>
	/// <param name="data">The data to be contained in the successful result.</param>
	/// <returns>A new successful Result instance.</returns>
	public static Result<T> Succeeded<T>(in T data) where T : notnull => Result<T>.Succeeded(data);
}

/// <summary>
///     Represents a reason for a failure in a Result.
/// </summary>
public interface IFailureReason;

/// <summary>
///     Represents a result that can either be successful with data of type T or failed with a reason.
/// </summary>
/// <typeparam name="T">The type of the result data. Must not be null.</typeparam>
public readonly record struct Result<T> where T : notnull
{
	/// <summary>
	///     The data contained in the result when successful.
	/// </summary>
	private readonly T _data;

	/// <summary>
	///     Initializes a new instance of the <see cref="Result{T}" /> struct with successful result.
	/// </summary>
	/// <param name="data">The data to be contained in the successful result.</param>
	/// <exception cref="ArgumentNullException">Thrown when data is null.</exception>
	private Result(in T data)
	{
		data.ThrowIfNull();

		Failure = null;
		_data   = data;
	}

	/// <summary>
	///     Initializes a new instance of the <see cref="Result{T}" /> struct with failure reason.
	/// </summary>
	/// <param name="reason">The reason for the failure.</param>
	/// <exception cref="ArgumentNullException">Thrown when reason is null.</exception>
	private Result(IFailureReason reason)
	{
		reason.ThrowIfNull();

		Failure = reason;
		_data   = default!;
	}

	/// <summary>
	///     Gets a value indicating whether the result is successful.
	/// </summary>
	public bool Success
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => Failure == null;
	}

	/// <summary>
	///     Gets the failure reason if the result is not successful.
	/// </summary>
	public IFailureReason? Failure { get; }

	/// <summary>
	///     Creates a failed result with the specified reason.
	/// </summary>
	/// <param name="reason">The reason for the failure.</param>
	/// <returns>A new failed Result instance.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Result<T> Failed(IFailureReason reason) => new(reason);

	/// <summary>
	///     Creates a successful result with the specified data.
	/// </summary>
	/// <param name="data">The data to be contained in the successful result.</param>
	/// <returns>A new successful Result instance.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Result<T> Succeeded(in T data) => new(data);

	/// <summary>
	///     Attempts to retrieve the payload without throwing.
	/// </summary>
	/// <remarks>
	///     Returns <c>true</c> when <see cref="Success" /> is <c>true</c>;
	///     the output <paramref name="data" /> is populated only in that case.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool TryGet(out T? data)
	{
		data = _data;
		return Success;
	}
}
