using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Bezoro.Core.Extensions;

namespace Bezoro.Core.Types;

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
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Result<T> Failed<T>(IFailureReason reason) where T : notnull => Result<T>.Failed(reason);

	/// <summary>
	///     Creates a successful result with the specified data.
	/// </summary>
	/// <typeparam name="T">The type of the result data.</typeparam>
	/// <param name="data">The data to be contained in the successful result.</param>
	/// <returns>A new successful Result instance.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Result<T> Succeeded<T>(in T data) where T : notnull => Result<T>.Succeeded(in data);
}

/// <summary>
///     Represents a reason for a failure in a Result.
/// </summary>
public interface IFailureReason;

/// <summary>
///     Represents a result that can either be successful with data of type T or failed with a reason.
/// </summary>
/// <typeparam name="T">The type of the result data. Must not be null.</typeparam>
[StructLayout(LayoutKind.Auto)]
[SkipLocalsInit]
public readonly struct Result<T> : IEquatable<Result<T>> where T : notnull
{
	/// <summary>
	///     Explicit flag to track whether this result was properly initialized as a success.
	/// </summary>
	private readonly bool _isSuccess;

	/// <summary>
	///     The failure reason if this result is a failure.
	/// </summary>
	private readonly IFailureReason? _failure;
	/// <summary>
	///     The data contained in the result when successful.
	/// </summary>
	private readonly T _data;

	/// <summary>
	///     Initializes a new instance of the <see cref="Result{T}" /> struct with successful result.
	/// </summary>
	/// <param name="data">The data to be contained in the successful result.</param>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private Result(in T data)
	{
		data.ThrowIfNull();

		_failure   = null;
		_data      = data;
		_isSuccess = true;
	}

	/// <summary>
	///     Initializes a new instance of the <see cref="Result{T}" /> struct with failure reason.
	/// </summary>
	/// <param name="reason">The reason for the failure.</param>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private Result(IFailureReason reason)
	{
		reason.ThrowIfNull();

		_failure   = reason;
		_data      = default!;
		_isSuccess = false;
	}

	/// <summary>
	///     Gets a value indicating whether the result is successful.
	///     Returns false for default-constructed instances.
	/// </summary>
	public bool Success
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _isSuccess;
	}

	/// <summary>
	///     Gets the failure reason if the result is not successful.
	/// </summary>
	public IFailureReason? Failure
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => _failure;
	}

	/// <summary>
	///     Equality operator.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator ==(Result<T> left, Result<T> right) => left.Equals(right);

	/// <summary>
	///     Implicitly converts a value to a successful Result.
	/// </summary>
	/// <param name="data">The data to wrap in a successful result.</param>
	/// <returns>A successful Result containing the data.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static implicit operator Result<T>(T data) => new(in data);

	/// <summary>
	///     Inequality operator.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator !=(Result<T> left, Result<T> right) => !left.Equals(right);

	#region Equality

	/// <inheritdoc />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Equals(Result<T> other) =>
		_isSuccess == other._isSuccess &&
		EqualityComparer<T>.Default.Equals(_data, other._data) &&
		Equals(_failure, other._failure);

	/// <inheritdoc />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override bool Equals(object? obj) => obj is Result<T> other && Equals(other);

	/// <inheritdoc />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override int GetHashCode() => HashCode.Combine(_isSuccess, _data, _failure);

	#endregion

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
	public static Result<T> Succeeded(in T data) => new(in data);

	/// <summary>
	///     Attempts to retrieve the payload without throwing.
	/// </summary>
	/// <param name="data">The data if successful; otherwise, the default value.</param>
	/// <returns><c>true</c> when <see cref="Success" /> is <c>true</c>; otherwise, <c>false</c>.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool TryGet([NotNullWhen(true)] out T? data)
	{
		data = _data;
		return _isSuccess;
	}

	/// <summary>
	///     Chains result-producing operations (flatMap/selectMany).
	/// </summary>
	/// <typeparam name="TNew">The target type.</typeparam>
	/// <param name="bind">The binding function that returns a new Result.</param>
	/// <returns>The result of the binding function, or the original failure.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public Result<TNew> Bind<TNew>(Func<T, Result<TNew>> bind) where TNew : notnull
	{
		bind.ThrowIfNull();

		if (!_isSuccess)
			return _failure is { }
					   ? Result<TNew>.Failed(_failure)
					   : default;

		return bind(_data);
	}

	/// <summary>
	///     Chains result-producing operations with state to avoid closure allocations.
	/// </summary>
	/// <typeparam name="TState">The type of the state parameter.</typeparam>
	/// <typeparam name="TNew">The target type.</typeparam>
	/// <param name="state">The state to pass to the bind function.</param>
	/// <param name="bind">The binding function that returns a new Result.</param>
	/// <returns>The result of the binding function, or the original failure.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public Result<TNew> Bind<TState, TNew>(TState state, Func<T, TState, Result<TNew>> bind) where TNew : notnull
	{
		bind.ThrowIfNull();

		if (!_isSuccess)
			return _failure is { }
					   ? Result<TNew>.Failed(_failure)
					   : default;

		return bind(_data, state);
	}

	/// <summary>
	///     Transforms the successful value using the specified function.
	/// </summary>
	/// <typeparam name="TNew">The target type.</typeparam>
	/// <param name="transform">The transformation function.</param>
	/// <returns>A new Result with the transformed value, or the original failure.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public Result<TNew> Map<TNew>(Func<T, TNew> transform) where TNew : notnull
	{
		transform.ThrowIfNull();

		if (!_isSuccess)
			return _failure is { }
					   ? Result<TNew>.Failed(_failure)
					   : default;

		return Result<TNew>.Succeeded(transform(_data));
	}

	/// <summary>
	///     Transforms the successful value using the specified function with state.
	///     Use this overload to avoid closure allocations.
	/// </summary>
	/// <typeparam name="TState">The type of the state parameter.</typeparam>
	/// <typeparam name="TNew">The target type.</typeparam>
	/// <param name="state">The state to pass to the transform function.</param>
	/// <param name="transform">The transformation function.</param>
	/// <returns>A new Result with the transformed value, or the original failure.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public Result<TNew> Map<TState, TNew>(TState state, Func<T, TState, TNew> transform) where TNew : notnull
	{
		transform.ThrowIfNull();

		if (!_isSuccess)
			return _failure is { }
					   ? Result<TNew>.Failed(_failure)
					   : default;

		return Result<TNew>.Succeeded(transform(_data, state));
	}

	/// <summary>
	///     Gets the value if successful, otherwise returns the specified default value.
	///     Zero allocation alternative to Match when you only need the value.
	/// </summary>
	/// <param name="defaultValue">The value to return if the result is a failure.</param>
	/// <returns>The success value or the default value.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public T GetValueOrDefault(T defaultValue) => _isSuccess ? _data : defaultValue;

	/// <summary>
	///     Gets the value if successful, otherwise returns default(T).
	/// </summary>
	/// <returns>The success value or default(T).</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public T? GetValueOrDefault() => _isSuccess ? _data : default;

	/// <summary>
	///     Pattern matches on the result, executing the appropriate function.
	/// </summary>
	/// <typeparam name="TResult">The return type.</typeparam>
	/// <param name="onSuccess">Function to execute when the result is successful.</param>
	/// <param name="onFailure">Function to execute when the result is a failure.</param>
	/// <returns>The result of the executed function.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<IFailureReason, TResult> onFailure)
	{
		onSuccess.ThrowIfNull();
		onFailure.ThrowIfNull();

		if (_isSuccess) return onSuccess(_data);

		if (_failure is null) ThrowInvalidOperationDefaultResult();

		return onFailure(_failure);
	}

	/// <summary>
	///     Pattern matches on the result with state to avoid closure allocations.
	/// </summary>
	/// <typeparam name="TState">The type of the state parameter.</typeparam>
	/// <typeparam name="TResult">The return type.</typeparam>
	/// <param name="state">The state to pass to both functions.</param>
	/// <param name="onSuccess">Function to execute when the result is successful.</param>
	/// <param name="onFailure">Function to execute when the result is a failure.</param>
	/// <returns>The result of the executed function.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public TResult Match<TState, TResult>(
		TState                                state,
		Func<T, TState, TResult>              onSuccess,
		Func<IFailureReason, TState, TResult> onFailure)
	{
		onSuccess.ThrowIfNull();
		onFailure.ThrowIfNull();

		if (_isSuccess) return onSuccess(_data, state);

		if (_failure is null) ThrowInvalidOperationDefaultResult();

		return onFailure(_failure, state);
	}

	/// <summary>
	///     Executes an action based on the result state.
	/// </summary>
	/// <param name="onSuccess">Action to execute when the result is successful.</param>
	/// <param name="onFailure">Action to execute when the result is a failure.</param>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Match(Action<T> onSuccess, Action<IFailureReason> onFailure)
	{
		onSuccess.ThrowIfNull();
		onFailure.ThrowIfNull();

		if (_isSuccess)
		{
			onSuccess(_data);
			return;
		}

		if (_failure is null) ThrowInvalidOperationDefaultResult();

		onFailure(_failure);
	}

	/// <summary>
	///     Executes an action based on the result state with state to avoid closure allocations.
	/// </summary>
	/// <typeparam name="TState">The type of the state parameter.</typeparam>
	/// <param name="state">The state to pass to both actions.</param>
	/// <param name="onSuccess">Action to execute when the result is successful.</param>
	/// <param name="onFailure">Action to execute when the result is a failure.</param>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Match<TState>(
		TState                         state,
		Action<T, TState>              onSuccess,
		Action<IFailureReason, TState> onFailure)
	{
		onSuccess.ThrowIfNull();
		onFailure.ThrowIfNull();

		if (_isSuccess)
		{
			onSuccess(_data, state);
			return;
		}

		if (_failure is null) ThrowInvalidOperationDefaultResult();

		onFailure(_failure, state);
	}

	[DoesNotReturn]
	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void ThrowInvalidOperationDefaultResult() =>
		throw new InvalidOperationException(
			"Cannot operate on a default-constructed Result<T>. Use ResultFactory.Succeeded() or ResultFactory.Failed() to create valid instances.");
}
