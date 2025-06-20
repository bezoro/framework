using System;
using System.Runtime.CompilerServices;

namespace Bezoro.Chess.API.Types
{
	public enum FailureReason
	{
		None,
		InvalidMove
	}

	public readonly struct Result<T> : IEquatable<Result<T>>
		where T : struct
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Result<T> Failed(FailureReason reason) => new(reason);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static Result<T> Succeeded(in T data) => new(data);

		public bool Success
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => Failure == FailureReason.None;
		}
		public FailureReason Failure { get; }

		public T Data { get; }

		#region Equality

		public static bool operator ==(Result<T> left, Result<T> right) => left.Equals(right);
		public static bool operator !=(Result<T> left, Result<T> right) => !left.Equals(right);

		public bool Equals(Result<T> other) =>
			Failure == other.Failure && Data.Equals(other.Data);

		public override bool Equals(object? obj) =>
			obj is Result<T> other && Equals(other);

		public override int GetHashCode() =>
			HashCode.Combine((int)Failure, Data);

		#endregion

		private Result(in T data)
		{
			Failure = FailureReason.None;
			Data    = data;
		}

		private Result(FailureReason reason)
		{
			Failure = reason;
			Data    = default;
		}
	}
}
