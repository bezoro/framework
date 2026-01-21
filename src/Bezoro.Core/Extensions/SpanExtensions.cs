using System.Runtime.CompilerServices;

namespace Bezoro.Core.Extensions;

/// <summary>
///     Provides extension methods for <see cref="Span{T}" /> and <see cref="ReadOnlySpan{T}" /> to enforce non-emptiness.
/// </summary>
public static class SpanExtensions
{
	/// <summary>
	///     Throws an <see cref="ArgumentException" /> if the <paramref name="span" /> sequence is empty.
	/// </summary>
	/// <typeparam name="T">The type of elements in the read-only span sequence.</typeparam>
	/// <param name="span">The <see cref="ReadOnlySpan{T}" /> instance to validate.</param>
	/// <param name="paramName">The expression or parameter name for the exception message (automatically supplied).</param>
	/// <returns>The same <see cref="ReadOnlySpan{T}" /> instance if it is not empty.</returns>
	/// <exception cref="ArgumentException">Thrown if <paramref name="span" /> is empty.</exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ReadOnlySpan<T> ThrowIfEmpty<T>(
		this                               ReadOnlySpan<T> span,
		[CallerArgumentExpression("span")] string?         paramName = null)
	{
		if (span.IsEmpty)
			throw new ArgumentException("Sequence cannot be empty.", paramName);

		return span;
	}

	/// <summary>
	///     Throws an <see cref="ArgumentException" /> if the <paramref name="span" /> sequence is empty.
	/// </summary>
	/// <typeparam name="T">The type of elements in the span sequence.</typeparam>
	/// <param name="span">The <see cref="Span{T}" /> instance to validate.</param>
	/// <param name="paramName">The expression or parameter name for the exception message (automatically supplied).</param>
	/// <returns>The same <see cref="Span{T}" /> instance if it is not empty.</returns>
	/// <exception cref="ArgumentException">Thrown if <paramref name="span" /> is empty.</exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Span<T> ThrowIfEmpty<T>(
		this                               Span<T> span,
		[CallerArgumentExpression("span")] string? paramName = null)
	{
		if (span.IsEmpty)
			throw new ArgumentException("Sequence cannot be empty.", paramName);

		return span;
	}
}
