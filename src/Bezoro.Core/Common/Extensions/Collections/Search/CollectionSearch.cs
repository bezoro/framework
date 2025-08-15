using System.Collections.Generic;

namespace Bezoro.Core.Common.Extensions.Collections.Search;

public static class CollectionSearch
{
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
