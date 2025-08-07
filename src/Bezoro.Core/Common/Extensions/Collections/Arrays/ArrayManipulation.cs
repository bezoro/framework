using System.Collections.Generic;
using System.Linq;
using static Bezoro.Core.Common.Helpers.ArrayHelper;
using Array = System.Array;

namespace Bezoro.Core.Common.Extensions.Collections.Arrays;

/// <summary>
///     Contains methods for array manipulation and modification.
/// </summary>
public static class ArrayManipulation
{
	/// <summary>
	///     Adds an item to an array, resizing if necessary.
	/// </summary>
	public static T[] Add<T>(this T[]? array, T item)
	{
		InitializeNullArray(ref array, 1);

		if (array.Length <= 0)
		{
			array    = new T[1];
			array[0] = item;
			return array;
		}

		int currentLength = array.Length;

		for (var i = 0; i < currentLength; i++)
		{
			if (!EqualityComparer<T>.Default.Equals(array[i], default)) continue;

			array[i] = item;
			return array;
		}

		int newCapacity;

		if (currentLength < 8)
			newCapacity = currentLength * 2;
		else if (currentLength < 64)
			newCapacity = (int)(currentLength * 1.5);
		else
			newCapacity = (int)(currentLength * 1.25);

		var newArray = new T[newCapacity];
		Array.Copy(array, newArray, currentLength);
		newArray[currentLength] = item;
		return newArray;
	}

	/// <summary>
	///     Adds a range of items to an array.
	/// </summary>
	public static T[] AddRange<T>(this T[] array, T[] items)
	{
		array.ThrowIfNull();
		items.ThrowIfNull();

		int newLength = array.Length + items.Length;
		var newArray  = new T[newLength];

		Array.Copy(array, newArray, array.Length);
		Array.Copy(items, 0,        newArray, array.Length, items.Length);
		return newArray;
	}

	/// <summary>
	///     Concatenates a variable number of arrays to a single array.
	/// </summary>
	public static T[] Concat<T>(this T[] array, params T[][] arrays)
	{
		array.ThrowIfNull();
		arrays.ThrowIfNull();

		var result = new T[array.Length];
		var index  = 0;

		Array.Copy(array, 0, result, index, array.Length);
		index += array.Length;

		foreach (var arr in arrays)
		{
			Array.Copy(arr, 0, result, index, arr.Length);
			index += arr.Length;
		}

		return result;
	}

	/// <summary>
	///     Returns a new array with the first occurrence of <paramref name="item" /> removed.
	///     If the item is not present, the original array instance is returned.
	/// </summary>
	public static T[] Remove<T>(this T[] array, T item)
	{
		if (array.Length == 0) return array;

		int idx = Array.IndexOf(array, item);
		if (idx < 0) return array;

		var result = new T[array.Length - 1];

		if (idx > 0)
			Array.Copy(array, 0, result, 0, idx);

		int itemsAfter = array.Length - idx - 1;
		if (itemsAfter > 0)
			Array.Copy(array, idx + 1, result, idx, itemsAfter);

		return result;
	}

	/// <summary>
	///     Removes all null elements from the array and returns a new array containing only non-null elements.
	/// </summary>
	public static T[] Trim<T>(this T[] array)
	{
		if (array == null) return Array.Empty<T>();

		int count = array.Count(x => x != null);

		if (count == array.Length) return array;

		var result = new T[count];
		var index  = 0;

		foreach (var t in array)
		{
			if (t != null)
				result[index++] = t;
		}

		return result;
	}

	public static void AddAt<T>(ref T[] array, T element, int i) where T : class
	{
		array.ThrowIfNull();
		element.ThrowIfNull();

		int emptyIndex = array.FindFirstEmptyIndex();
		array[emptyIndex] = element;
	}

	/// <summary>
	///     Clears all elements in the array by setting them to their default values.
	/// </summary>
	public static void Clear<T>(this T[] array)
	{
		if (array == null) return;

		Array.Clear(array, 0, array.Length);
	}

	/// <summary>
	///     Clears all elements in the 2D array by setting them to their default values.
	/// </summary>
	public static void Clear<T>(this T[,] array)
	{
		if (array == null) return;

		Array.Clear(array, 0, array.Length);
	}
}
