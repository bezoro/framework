using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Bezoro.Core.Common.Extensions.Collections.Check;

/// <summary>
///     Provides extension methods for <see cref="ICollection{T}" />.
/// </summary>
public static class CollectionCheckExtensions
{
	/// <summary>
	///     Determines whether a collection is empty.
	/// </summary>
	/// <typeparam name="T">The type of elements in the collection.</typeparam>
	/// <param name="collection">The collection to check.</param>
	/// <returns><c>true</c> if the collection is empty; otherwise, <c>false</c>.</returns>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="collection" /> is <c>null</c>.</exception>
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
	/// <remarks>This method is particularly useful for null checks before performing operations on collections.</remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsNull<T>([NotNullWhen(false)] this ICollection<T?>? collection) => collection is null;

	/// <summary>
	///     Determines whether the specified collection is null or contains no elements.
	/// </summary>
	/// <typeparam name="T">The type of elements in the collection.</typeparam>
	/// <param name="collection">The collection to check.</param>
	/// <returns><c>true</c> if the collection is null or contains no elements; otherwise, <c>false</c>.</returns>
	/// <remarks>This method combines both null check and empty check in a single operation.</remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsNullOrEmpty<T>([NotNullWhen(false)] this ICollection<T?>? collection) =>
		collection is null || collection.Count is 0;

	/// <summary>
	///     Validates that the specified collection contains at least one element by throwing an exception if it's empty.
	/// </summary>
	/// <typeparam name="T">The type of elements in the collection.</typeparam>
	/// <param name="collection">The collection to validate.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="collection" /> is <c>null</c>.</exception>
	/// <exception cref="EmptyCollectionException">Thrown when the collection contains no elements.</exception>
	/// <remarks>This method is useful for validating collection parameters in methods that require non-empty collections.</remarks>
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
