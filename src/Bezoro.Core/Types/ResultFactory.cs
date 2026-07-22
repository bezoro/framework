using System.Runtime.CompilerServices;

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
