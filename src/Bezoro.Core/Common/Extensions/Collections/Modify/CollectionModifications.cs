using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Bezoro.Core.Common.Extensions.Collections.Check;

namespace Bezoro.Core.Common.Extensions.Collections.Modify;

public static class CollectionModifications
{
	/// <summary>
	///     Adds a range of items to a collection.
	/// </summary>
	/// <typeparam name="T">The type of elements in the collection.</typeparam>
	/// <param name="collection">The collection to add the items to.</param>
	/// <param name="items">The items to add.</param>
	/// <remarks>
	///     If <paramref name="items" /> is <c>null</c>, the method returns without adding any items.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void AddRange<T>(this ICollection<T> collection, ICollection<T> items)
	{
		CollectionChecks.ThrowIfEmpty(collection.ThrowIfNull());
		CollectionChecks.ThrowIfEmpty(items.ThrowIfNull());

		foreach (var item in items)
			collection.Add(item);
	}
}
