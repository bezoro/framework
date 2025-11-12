using System;
using System.Runtime.CompilerServices;

namespace Bezoro.Core.Common.Extensions;

public static class MemoryExtensions
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static Memory<T> ThrowIfEmpty<T>(
		this Memory<T> memory,
		[CallerArgumentExpression("memory")] string? paramName = null)
	{
		if (memory.Length == 0)
			throw new ArgumentException("Sequence cannot be empty.", paramName);

		return memory;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ReadOnlyMemory<T> ThrowIfEmpty<T>(
		this ReadOnlyMemory<T> memory,
		[CallerArgumentExpression("memory")] string? paramName = null)
	{
		if (memory.Length == 0)
			throw new ArgumentException("Sequence cannot be empty.", paramName);

		return memory;
	}
}

