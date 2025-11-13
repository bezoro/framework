using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Bezoro.Core.Common.Extensions.Collections.Modify;

/// <summary>
///     Provides extension methods for modifying collections.
/// </summary>
/// <remarks>
///     This class contains methods that help modify collections in various ways,
///     such as adding multiple items at once.
/// </remarks>
public static class CollectionModifyExtensions
{
	/// <summary>
	///     Adds a range of items to a collection.
	/// </summary>
	/// <typeparam name="T">The type of elements in the collection.</typeparam>
	/// <param name="collection">The collection to add the items to.</param>
	/// <param name="items">The items to add.</param>
	/// <remarks>
	///     This method validates both the source collection and items collection for null and empty states before performing
	///     the operation. The source collection must allow modifications, and sufficient capacity should be available to add
	///     all items. Items are added sequentially in the order they appear in the input collection.
	/// </remarks>
	/// <example>
	///     <code>
	/// 	 var sourceList = new List&lt;int&gt; { 1, 2, 3 };
	/// 	 var itemsToAdd = new List&lt;int&gt; { 4, 5, 6 };
	/// 	 sourceList.AddRange(itemsToAdd); // sourceList now contains: 1, 2, 3, 4, 5, 6
	/// 	 </code>
	/// </example>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void AddRange<T>(this ICollection<T> collection, ICollection<T> items)
	{
		collection.ThrowIfNull();
		items.ThrowIfNull();

		if (items.Count == 0) return;

		foreach (var item in items)
			collection.Add(item);
	}
}
