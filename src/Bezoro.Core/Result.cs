using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Bezoro.Core
{
	public enum FailureReason
	{
		None,
		InvalidMove
	}

	public static class Result
	{
		public static Result<T> Failed<T>(FailureReason reason) => Result<T>.Failed(reason);
		public static Result<T> Succeeded<T>(in T data) => Result<T>.Succeeded(data);
	}
	
	/// <summary>
	///     Represents a result that can either be successful with data of type T or failed with a reason.
	/// </summary>
	/// <typeparam name="T">The type of the result data. Must not be null.</typeparam>
	public readonly struct Result<T> : IEquatable<Result<T>>
	{
		private readonly T? _data;

		private Result(in T data)
		{
			Failure = FailureReason.None;
			_data   = data;
		}

		private Result(FailureReason reason)
		{
			Failure = reason;
			_data   = default;
		}

		/// <summary>
		///     Gets a value indicating whether the result is successful.
		/// </summary>
		public bool Success
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Failure == FailureReason.None;
		}

		/// <summary>
		///     Gets the failure reason if the result is not successful.
		/// </summary>
		public FailureReason Failure { get; }

		/// <summary>
		///     Determines whether two specified Result instances are equal.
		/// </summary>
		public static bool operator ==(Result<T> left, Result<T> right) => left.Equals(right);

		/// <summary>
		///     Determines whether two specified Result instances are not equal.
		/// </summary>
		public static bool operator !=(Result<T> left, Result<T> right) => !left.Equals(right);

		#region Equality

		/// <summary>
		///     Determines whether the specified Result is equal to the current Result.
		/// </summary>
		public bool Equals(Result<T> other) =>
			Failure == other.Failure &&
			EqualityComparer<T?>.Default.Equals(_data, other._data);

		/// <summary>
		///     Determines whether the specified object is equal to the current Result.
		/// </summary>
		public override bool Equals(object? obj) =>
			obj is Result<T> other && Equals(other);

		/// <summary>
		///     Returns the hash code for this Result.
		/// </summary>
		public override int GetHashCode() =>
			HashCode.Combine((int)Failure, EqualityComparer<T?>.Default.GetHashCode(_data));

		#endregion

		/// <summary>
		///     Creates a failed result with the specified reason.
		/// </summary>
		/// <param name="reason">The reason for the failure.</param>
		/// <returns>A new failed Result instance.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Result<T> Failed(FailureReason reason) => new(reason);

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
}
