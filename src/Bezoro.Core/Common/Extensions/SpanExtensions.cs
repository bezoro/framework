using System;
using System.Runtime.CompilerServices;

namespace Bezoro.Core.Common.Extensions;

public static class SpanExtensions
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Span<T> ThrowIfEmpty<T>(
		this Span<T> span,
		[CallerArgumentExpression("span")] string? paramName = null)
	{
		if (span.IsEmpty)
			throw new ArgumentException("Sequence cannot be empty.", paramName);

		return span;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ReadOnlySpan<T> ThrowIfEmpty<T>(
		this ReadOnlySpan<T> span,
		[CallerArgumentExpression("span")] string? paramName = null)
	{
		if (span.IsEmpty)
			throw new ArgumentException("Sequence cannot be empty.", paramName);

		return span;
	}
}

