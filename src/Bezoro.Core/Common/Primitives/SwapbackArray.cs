using System;
using System.Collections.Generic;

namespace Bezoro.Core.Common.Primitives;

/// <summary>
///     Provides a dynamic array that allows for efficient addition of elements and removal
///     by swapping the last element into the vacancy caused by the removal action. Suitable
///     for use cases where order preservation of elements is not required. This implementation
///     automatically manages internal capacity and provides O(1) removal operations.
/// </summary>
/// <typeparam name="T">Specifies the type of elements stored in the array.</typeparam>
/// <remarks>
///     The array maintains efficiency by moving the last element into the position of
///     any removed element, avoiding the need to shift subsequent elements. This makes
///     removal operations O(1) but does not preserve element ordering.
/// </remarks>
public class SwapbackArray<T>
{
	private const int MINIMUM_ARRAY_SIZE = 4;

	/// <summary>
	///     Represents the current number of elements in the <see cref="SwapbackArray{T}" />.
	/// </summary>
	private int _count;

	/// <summary>
	///     Internal storage array for the elements of the <see cref="SwapbackArray{T}" />.
	///     It dynamically resizes as elements are added or removed. The capacity is adjusted
	///     to optimize memory usage and access efficiency. This array supports reordering
	///     of elements when removing items.
	/// </summary>
	private T?[] _items;

	/// <summary>
	///     Initializes a new instance of the <see cref="SwapbackArray{T}" /> class.
	/// </summary>
	/// <param name="initialCapacity">The initial capacity of the array. Must be non-negative.</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when initialCapacity is negative.</exception>
	public SwapbackArray(int initialCapacity = MINIMUM_ARRAY_SIZE)
	{
		if (initialCapacity < 0)
			throw new ArgumentOutOfRangeException(nameof(initialCapacity), "Initial capacity cannot be negative.");

		int actualCapacity = Math.Max(initialCapacity, MINIMUM_ARRAY_SIZE);

		_items = new T[actualCapacity];
		_count = 0;

		Logger.LogSuccess($"SwapbackArray initialized with capacity: {actualCapacity}");
	}


	/// <summary>
	///     Gets the total number of elements that the internal array can hold without resizing.
	/// </summary>
	public int Capacity => _items.Length;

	/// <summary>
	///     Gets the number of elements currently contained in the <see cref="SwapbackArray{T}" />.
	/// </summary>
	public int Count => _count;

	/// <summary>
	///     Removes the first occurrence of the specified item using EqualityComparer&lt;T&gt;.Default.
	/// </summary>
	/// <param name="item">The item to remove from the array.</param>
	/// <returns>true if item was successfully removed; otherwise, false.</returns>
	/// <remarks>
	///     This method performs a linear search through the array to find the specified item.
	///     When found, the item is removed by replacing it with the last element in the array.
	/// </remarks>
	public bool Remove(T item)
	{
		var comparer = EqualityComparer<T>.Default;
		for (var i = 0; i < _count; i++)
		{
			if (comparer.Equals(_items[i]!, item))
				return RemoveAt(i);
		}

		Logger.LogWarning("Attempted to remove an item that does not exist in the array.");
		return false;
	}

	/// <summary>
	///     Removes the element at the specified index by swapping the last element into that position.
	///     Returns false if the index is invalid.
	/// </summary>
	public bool RemoveAt(int index)
	{
		if (index < 0 || index >= _count)
		{
			Logger.LogError($"Attempted to remove at invalid index: {index}");
			return false;
		}

		PerformSwapAndTrim(index);

		// Downsize if array is underutilized
		if (!ShouldResize(_count, _items.Length)) return true;

		int newSize = Math.Max(_items.Length / 2, MINIMUM_ARRAY_SIZE);
		Resize(newSize);

		return true;
	}

	/// <summary>
	///     Attempts to retrieve the value at the specified index in the dynamic array.
	/// </summary>
	/// <param name="index">The zero-based index of the element to retrieve.</param>
	/// <param name="value">
	///     The variable where the retrieved value will be stored if the index is valid; otherwise, the default
	///     value of the type.
	/// </param>
	/// <returns>
	///     A boolean value indicating whether the retrieval was successful.
	/// </returns>
	public bool TryGet(int index, out T? value)
	{
		if (index < 0 || index >= _count)
		{
			Logger.LogError($"Attempted to access invalid index: {index}");
			value = default;
			return false;
		}

		value = _items[index];
		Logger.LogSuccess($"Item retrieved at index {index}: {value}");
		return true;
	}

	/// <summary>
	///     Adds an item to the end of the SwapbackArray. If the array's capacity is insufficient,
	///     its size is doubled to accommodate the new element.
	/// </summary>
	/// <param name="item">The item to add to the SwapbackArray.</param>
	public void Add(T item)
	{
		if (_count == _items.Length)
		{
			Logger.LogWarning("Array capacity reached. Resizing...");
			Resize(_items.Length * 2);
			Logger.LogSuccess($"Array resized to new capacity: {_items.Length}");
		}

		_items[_count++] = item;
		Logger.LogSuccess($"Item added. Current count: {_count}");
	}

	/// <summary>
	///     Removes all elements from the array.
	/// </summary>
	public void Clear()
	{
		Logger.LogInfo("Clearing all elements from the array.", this, LogCategory.Utilities);
		Array.Clear(_items, 0, _count);
		_count = 0;
		Logger.LogSuccess("Array cleared.");

		// Trim capacity back to minimum to free memory
		if (Capacity <= MINIMUM_ARRAY_SIZE) return;

		Resize(MINIMUM_ARRAY_SIZE);
		Logger.LogSuccess("Capacity trimmed to minimum after clearing.");
	}

	private static bool ShouldResize(int currentCount, int currentCapacity) =>
		currentCount <= currentCapacity / 4 && currentCapacity > MINIMUM_ARRAY_SIZE;

	private void PerformSwapAndTrim(int index)
	{
		Logger.LogInfo($"Removing item at index {index}.", this, LogCategory.Utilities);

		// Replace the item at the given index with the last item (swap-and-trim)
		_items[index] = _items[_count - 1];

		// Clear the last item and reduce the count
		_items[--_count] = default;
		Logger.LogSuccess($"Item removed. Current count: {_count}");
	}

	/// <summary>
	///     Resizes the internal array to a new specified size, preserving existing elements.
	/// </summary>
	/// <param name="newSize">The new size of the array.</param>
	private void Resize(int newSize)
	{
		var newItems = new T[newSize];
		Array.Copy(_items, newItems, _count);
		_items = newItems;

		Logger.LogSuccess($"Resize operation complete. New size: {newSize}");
	}
}
