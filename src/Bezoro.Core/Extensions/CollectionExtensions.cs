using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Bezoro.Core.Types.Exceptions;

namespace Bezoro.Core.Extensions;

/// <summary>
///     Provides extension methods for <see cref="ICollection{T}" />, including checking, modification, and searching.
/// </summary>
public static class CollectionExtensions
{
	#region Search

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

	#endregion

	#region Modify

	/// <summary>
	///     Adds a range of items to a collection.
	/// </summary>
	/// <typeparam name="T">The type of elements in the collection.</typeparam>
	/// <param name="collection">The collection to add the items to.</param>
	/// <param name="items">The items to add.</param>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void AddRange<T>(this ICollection<T> collection, ICollection<T> items)
	{
		collection.ThrowIfNull();
		items.ThrowIfNull();

		if (items.Count == 0) return;

		foreach (var item in items)
			collection.Add(item);
	}

	#endregion

	#region Check

	/// <summary>
	///     Determines whether a collection is empty.
	/// </summary>
	/// <typeparam name="T">The type of elements in the collection.</typeparam>
	/// <param name="collection">The collection to check.</param>
	/// <returns><c>true</c> if the collection is empty; otherwise, <c>false</c>.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsEmpty<T>(this ICollection<T> collection)
	{
		collection.ThrowIfNull();
		return collection.Count == 0;
	}

	/// <summary>
	///     Determines whether the specified collection reference is null.
	/// </summary>
	/// <typeparam name="T">The type of elements in the collection.</typeparam>
	/// <param name="collection">The collection reference to check.</param>
	/// <returns><c>true</c> if the collection reference is null; otherwise, <c>false</c>.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsNull<T>([NotNullWhen(false)] this ICollection<T?>? collection) => collection is null;

	/// <summary>
	///     Determines whether the specified collection is null or contains no elements.
	/// </summary>
	/// <typeparam name="T">The type of elements in the collection.</typeparam>
	/// <param name="collection">The collection to check.</param>
	/// <returns><c>true</c> if the collection is null or contains no elements; otherwise, <c>false</c>.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsNullOrEmpty<T>([NotNullWhen(false)] this ICollection<T?>? collection) =>
		collection is null || collection.Count is 0;

	/// <summary>
	///     Validates that the specified collection contains at least one element by throwing an exception if it's empty.
	/// </summary>
	/// <typeparam name="T">The type of elements in the collection.</typeparam>
	/// <param name="collection">The collection to validate.</param>
	/// <param name="paramName">The name of the parameter (optional).</param>
	/// <returns>The non-empty <paramref name="collection" />.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ICollection<T> ThrowIfEmpty<T>(
		this                                     ICollection<T> collection,
		[CallerArgumentExpression("collection")] string?        paramName = null)
	{
		collection.ThrowIfNull(paramName);

		if (collection.Count == 0) throw new EmptyCollectionException(paramName ?? nameof(collection));

		return collection;
	}

	#endregion
}
