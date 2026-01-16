namespace Bezoro.Core.Common.Extensions.Collections.Search;

/// <summary>
///     Contains methods for searching and counting elements in arrays.
/// </summary>
public static class ArraySearchExtensions
{
	/// <summary>
	///     Searches for the specified element in the array using Equals method and returns its index.
	/// </summary>
	/// <typeparam name="T">The type of elements in the array. Must be a reference type.</typeparam>
	/// <param name="array">The array to search in. Cannot be null.</param>
	/// <param name="element">The element to find. Cannot be null.</param>
	/// <param name="index">
	///     When this method returns, contains the zero-based index of the found element in the array, or -1 if the element was
	///     not
	///     found.
	/// </param>
	/// <returns>true if the element was found; otherwise, false.</returns>
	public static bool Contains<T>(this T[] array, T element, out int index)
		where T : class
	{
		array.ThrowIfNull();
		element.ThrowIfNull();

		index = -1;

		for (var i = 0; i < array.Length; i++)
		{
			bool isMatch = array[i].Equals(element);
			if (!isMatch) continue;

			index = i;
			return true;
		}

		return false;
	}

	/// <summary>
	///     Searches for the specified struct element in the array and returns its index.
	/// </summary>
	/// <typeparam name="T">The type of elements in the array. Must be a value type implementing IEquatable{T}.</typeparam>
	/// <param name="array">The array to search in.</param>
	/// <param name="element">The element to find.</param>
	/// <param name="index">
	///     When this method returns, contains the index of the found element, or -1 if the element was not
	///     found.
	/// </param>
	/// <returns>true if the element was found; otherwise, false.</returns>
	public static bool ContainsStruct<T>(this T[] array, T element, out int index)
		where T : struct, IEquatable<T>
	{
		array.ThrowIfNull();
		element.ThrowIfNull();

		index = -1;

		for (var i = 0; i < array.Length; i++)
		{
			if (!array[i].Equals(element)) continue;

			index = i;
			return true;
		}

		return false;
	}

	/// <summary>
	///     Attempts to find the first null element in the array and returns its index.
	/// </summary>
	/// <typeparam name="T">The type of elements in the array. Must be a reference type.</typeparam>
	/// <param name="array">The array to search in.</param>
	/// <param name="index">
	///     When this method returns, contains the index of the first null element, or -1 if no null elements
	///     were found.
	/// </param>
	/// <returns>true if a null element was found; otherwise, false.</returns>
	public static bool TryFindFirstEmptyIndex<T>(this T?[] array, out int index)
		where T : class
	{
		array.ThrowIfNull();
		index = -1;

		if (array.Length == 0) return false;

		for (var i = 0; i < array.Length; i++)
		{
			var element = array[i];
			if (!element.IsNull()) continue;

			index = i;
			return true;
		}

		return false;
	}

	/// <summary>
	///     Counts the number of empty (null) indices in the array.
	/// </summary>
	public static int CountEmptyIndices<T>(this T?[] array)
		where T : class
	{
		array.ThrowIfNull();

		var emptyIndicesCount = 0;

		foreach (var t in array)
		{
			if (t.IsNull())
				emptyIndicesCount++;
		}

		return emptyIndicesCount;
	}

	/// <summary>
	///     Counts the number of empty indices based on a valid size.
	/// </summary>
	public static int CountEmptyIndices<T>(this T[] array, int validSize)
		where T : class
	{
		array.ThrowIfNull();
		validSize.ThrowIfLessThan(1);
		validSize.ThrowIfMoreThan(array.Length);

		return array.Length - validSize;
	}

	/// <summary>
	///     Counts the number of filled (non-null) indices in the array.
	/// </summary>
	public static int CountFilledIndices<T>(this T?[] array)
		where T : class
	{
		array.ThrowIfNull();

		var filledIndicesCount = 0;

		foreach (var t in array)
		{
			if (!t.IsNull())
				filledIndicesCount++;
		}

		return filledIndicesCount;
	}
}
