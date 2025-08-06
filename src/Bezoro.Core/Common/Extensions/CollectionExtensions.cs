using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Bezoro.Core.Common.Extensions;

public static class CollectionExtensions
{
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsEmpty<T>([NotNullWhen(false)] this ICollection<T?>? collection) =>
		collection == null || collection.Count == 0;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsNull<T>([NotNullWhen(false)] this ICollection<T?>? collection) => collection == null;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsNullOrEmpty<T>([NotNullWhen(false)] this ICollection<T?>? collection) =>
		collection == null || collection.Count == 0;

	/// <summary>
	///     Returns the number of items in <paramref name="collection" /> or 0 when it is <c>null</c>.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static int SafeCount<T>(this ICollection<T?>? collection) => collection?.Count ?? 0;

	/// <summary>
	///     Adds all items from <paramref name="items" /> to <paramref name="collection" />.
	///     When <paramref name="items" /> is <c>null</c> the call is silently ignored.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void AddRange<T>(this ICollection<T> collection, IEnumerable<T>? items)
	{
		if (collection == null) throw new ArgumentNullException(nameof(collection));

		if (items == null) return;

		foreach (var item in items)
			collection.Add(item);
	}
}
