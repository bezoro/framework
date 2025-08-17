using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

// ReSharper disable PossibleMultipleEnumeration

namespace Bezoro.Core.Common.Extensions;

public static class EnumerableExtensions
{
	/// <summary>
	///     Returns <c>true</c> if <paramref name="source" /> contains at least one element,
	///     using <see cref="TryGetCount(IEnumerable,out int)" /> when possible to avoid enumeration.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool HasAny(this IEnumerable source)
	{
		if (source is null) throw new ArgumentNullException(nameof(source));

		if (source is ICollection c) return c.Count != 0;
		if (source is string s) return s.Length     != 0;

		foreach (object? _ in source)
			return true;

		return false;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool HasAny<T>(this IEnumerable<T> source) => HasAny((IEnumerable)source);

	/// <summary>
	///     Returns <c>true</c> when the sequence is <c>null</c> or has no elements.
	///     Enumerates at most one element when the count cannot be obtained cheaply.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsNullOrEmpty(this IEnumerable? source)
		=> source is null || !source.HasAny();

	/// <summary>
	///     Returns <c>true</c> when the sequence is <c>null</c> or has no elements.
	///     Enumerates at most one element when the count cannot be obtained cheaply.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsNullOrEmpty<T>(this IEnumerable<T>? source)
		=> source is null || !source.HasAny();

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool TryGetCount(this IEnumerable source, out int count)
	{
		switch (source)
		{
			case ICollection c:
				count = c.Count;
				return true;

			case string s: // IEnumerable<char>
				count = s.Length;
				return true;

			default:
				count = -1;
				return false;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool TryGetCount<T>(this IEnumerable<T> source, out int count)
	{
		switch (source)
		{
			case ICollection<T> c:
				count = c.Count;
				return true;

			case IReadOnlyCollection<T> ro:
				count = ro.Count;
				return true;

			case ICollection nonGeneric:
				count = nonGeneric.Count;
				return true;

			// Special-case strings when T == char
			case string s when typeof(T) == typeof(char):
				count = s.Length;
				return true;

			default:
				count = -1;
				return false;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static string PrettyJoin<T>(this IEnumerable<T> source, string separator = ", ")
		=> string.Join(separator, source);
}
