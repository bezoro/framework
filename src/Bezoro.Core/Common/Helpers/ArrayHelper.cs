using System;
using Bezoro.Core.Common.Extensions;

namespace Bezoro.Core.Common.Helpers;

public static class ArrayHelper
{
	public static bool CompareArrays<T>(T[] first, T[] second)
	{
		ValidateArray(first);
		ValidateArray(second);

		if (first.Length != second.Length) return false;

		for (var i = 0; i < first.Length; i++)
		{
			if (!Equals(first[i], second[i]))
				return false;
		}

		return true;
	}

	public static T?[] RemoveElement<T>(ref T?[] array, T item) where T : class
	{
		ValidateArray(array);

		int index                    = Array.IndexOf(array, item);
		if (index >= 0) array[index] = null;

		return array;
	}

	public static T[] Add<T>(ref T[] array, T item)
	{
		ValidateArray(array);

		return TryAddToFirstEmptySlot(ref array, item) ? array : throw new ArrayIsFullException();
	}

	public static T[] AddUnique<T>(ref T[] array, T item)
	{
		ValidateArray(array);

		if (Array.IndexOf(array, item) >= 0 || TryAddToFirstEmptySlot(ref array, item))
			return array;

		throw new ArrayIsFullException();
	}

	private static bool TryAddToFirstEmptySlot<T>(ref T[] array, T item)
	{
		int count = array.Length;
		for (var i = 0; i < count; i++)
		{
			if (!array[i].IsNull()) continue;

			array[i] = item;
			return true;
		}

		return false;
	}

	private static void ValidateArray<T>(T[] array)
	{
		array.ThrowIfNull().ThrowIfEmpty();
	}
}

public sealed class ArrayIsFullException() : Exception("The array is full and cannot accept more items.");
