namespace Bezoro.Core.Common.Extensions.Collections.Search;

/// <summary>
///     Contains extension methods for searching elements in collections.
/// </summary>
public static class CollectionSearchExtensions
{
	/// <summary>
	///     Searches for the specified element in the collection using the default equality comparer.
	/// </summary>
	/// <typeparam name="T">The type of elements in the collection.</typeparam>
	/// <param name="collection">The collection to search in. Cannot be null or empty.</param>
	/// <param name="value">The value to find.</param>
	/// <returns>The found element if it exists; otherwise, the default value of type T.</returns>
	public static T Find<T>(this ICollection<T> collection, T value)
	{
		collection.ThrowIfNull().ThrowIfEmpty();

		var comparer = EqualityComparer<T>.Default;

		foreach (var item in collection)
		{
			if (comparer.Equals(item, value))
				return item;
		}

		return default!;
	}
}
