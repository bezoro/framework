using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Bezoro.Core.Common.Extensions.Collections;

/// <summary>
///     Provides extension methods for <see cref="ICollection{T}" />.
/// </summary>
public static class CollectionExtensions
{
	/// <summary>
	///     Determines whether a collection is null.
	/// </summary>
	/// <typeparam name="T">The type of elements in the collection.</typeparam>
	/// <param name="collection">The collection to check.</param>
	/// <returns><c>true</c> if the collection is null; otherwise, <c>false</c>.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsNull<T>([NotNullWhen(false)] this ICollection<T?>? collection) => collection is null;

	/// <summary>
	///     Determines whether a collection is null or empty.
	/// </summary>
	/// <typeparam name="T">The type of elements in the collection.</typeparam>
	/// <param name="collection">The collection to check.</param>
	/// <returns><c>true</c> if the collection is null or empty; otherwise, <c>false</c>.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsNullOrEmpty<T>([NotNullWhen(false)] this ICollection<T?>? collection) =>
		collection is null || collection.Count is 0;

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
		collection.ThrowIfNull();
		collection.ThrowIfEmpty();
		items.ThrowIfNull();
		items.ThrowIfEmpty();

		foreach (var item in items)
			collection.Add(item);
	}

	/// <summary>
	///     Throws an <see cref="EmptyCollectionException" /> if the collection is empty.
	/// </summary>
	/// <typeparam name="T">The type of elements in the collection.</typeparam>
	/// <param name="collection">The collection to check.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="collection" /> is <c>null</c>.</exception>
	/// <exception cref="EmptyCollectionException">Thrown when the collection is empty.</exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void ThrowIfEmpty<T>(this ICollection<T> collection)
	{
		collection.ThrowIfNull();

		if (collection.Count == 0) throw new EmptyCollectionException(nameof(collection));
	}
}

/// <summary>
///     Represents an error that occurs when a collection is empty.
/// </summary>
public class EmptyCollectionException : Exception
{
	/// <summary>
	///     Initializes a new instance of the <see cref="EmptyCollectionException" /> class.
	/// </summary>
	/// <param name="collectionName">The name of the collection that is empty.</param>
	public EmptyCollectionException(string collectionName) : base(
		string.IsNullOrWhiteSpace(collectionName)
			? "The collection is empty."
			: $"The collection '{collectionName}' is empty.") { }
}
