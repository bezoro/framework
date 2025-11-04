using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

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
public class SwapbackArray<T> : IEnumerable<T>
{
	private const int MAX_ARRAY_LENGTH   = 0x7FFFFFC7; // match CLR array max length heuristic
	private const int MINIMUM_ARRAY_SIZE = 4;

	/// <summary>
	///     Represents the current number of elements in the <see cref="SwapbackArray{T}" />.
	/// </summary>
	private int _count;
	private int _version;

	/// <summary>
	///     Internal storage array for the elements of the <see cref="SwapbackArray{T}" />.
	///     It dynamically resizes as elements are added or removed. The capacity is adjusted
	///     to optimize memory usage and access efficiency. This array supports reordering
	///     of elements when removing items.
	/// </summary>
	private T[] _items;

	/// <summary>
	///     Initializes a new instance of the <see cref="SwapbackArray{T}" /> class.
	/// </summary>
	/// <param name="initialCapacity">The initial capacity of the array. Must be non-negative.</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when initialCapacity is negative.</exception>
	public SwapbackArray(int initialCapacity = MINIMUM_ARRAY_SIZE)
	{
		if (initialCapacity < MINIMUM_ARRAY_SIZE)
			throw new ArgumentOutOfRangeException(
				nameof(initialCapacity),
				$"Initial capacity cannot <= {MINIMUM_ARRAY_SIZE}.");

		int actualCapacity = Math.Max(initialCapacity, MINIMUM_ARRAY_SIZE);

		_items = new T[actualCapacity];
		_count = 0;
	}

	public SwapbackArray(ICollection<T> values)
	{
		_items = new T[values.Count];
		_count = values.Count;
		values.CopyTo(_items, 0);
	}


	/// <summary>
	///     Gets the total number of elements that the internal array can hold without resizing.
	/// </summary>
	public int Capacity => _items.Length;

	/// <summary>
	///     Gets the number of elements currently contained in the <see cref="SwapbackArray{T}" />.
	/// </summary>
	public int Count => _count;

	public T this[int index]
	{
		get => (uint)index >= (uint)_count ? throw new ArgumentOutOfRangeException(nameof(index)) : _items[index];
		set
		{
			if ((uint)index >= (uint)_count) throw new ArgumentOutOfRangeException(nameof(index));

			_items[index] = value;
		}
	}

	public bool Contains(T item)
	{
		var comparer = EqualityComparer<T>.Default;
		for (var i = 0; i < _count; i++)
		{
			if (comparer.Equals(_items[i], item))
				return true;
		}

		return false;
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
	public bool TryGet(int index, [MaybeNullWhen(false)] out T value)
	{
		if (index < 0)
			throw new ArgumentOutOfRangeException(nameof(index), index, "Index must be zero or positive.");

		if (index >= _count)
		{
			value = default!;
			return false;
		}

		value = _items[index];
		return true;
	}

	/// <summary>
	///     Removes the first occurrence of the specified item using EqualityComparer&lt;T&gt;.Default.
	/// </summary>
	/// <param name="item">The item to remove from the array.</param>
	/// <returns>true if item was successfully removed; otherwise, false.</returns>
	/// <remarks>
	///     This method performs a linear search through the array to find the specified item.
	///     When found, the item is removed by replacing it with the last element in the array.
	/// </remarks>
	public bool TryRemove(T item)
	{
		var comparer = EqualityComparer<T>.Default;
		for (var i = 0; i < _count; i++)
		{
			if (comparer.Equals(_items[i], item))
				return TryRemoveAt(i);
		}

		return false;
	}

	/// <summary>
	///     Removes the element at the specified index by swapping the last element into that position.
	///     Returns false if the index is invalid.
	/// </summary>
	public bool TryRemoveAt(int index)
	{
		if (index < 0 || index >= _count)
			throw new ArgumentOutOfRangeException(nameof(index), index, $"Index must be between 0 and {_count - 1}.");

		// swap last into index and trim
		_items[index]    = _items[_count - 1];
		_items[--_count] = default!;

		MaybeShrink();
		return true;
	}

	public IEnumerator<T> GetEnumerator()
	{
		int version = _version;
		for (var i = 0; i < _count; i++)
		{
			if (version != _version)
				throw new InvalidOperationException("Collection was modified during enumeration.");

			yield return _items[i];
		}
	}

	public ReadOnlySpan<T> AsSpan() => new(_items, 0, _count);

	public T[] ToArray()
	{
		var result = new T[_count];
		Array.Copy(_items, 0, result, 0, _count);
		return result;
	}

	/// <summary>
	///     Adds an item to the end of the SwapbackArray. If the array's capacity is insufficient,
	///     its size is doubled to accommodate the new element.
	/// </summary>
	/// <param name="item">The item to add to the SwapbackArray.</param>
	public void Add(T item)
	{
		EnsureCapacity(_count + 1);
		_items[_count++] = item;
		_version++;
	}

	/// <summary>
	///     Removes all elements from the array.
	/// </summary>
	public void Clear()
	{
		Array.Clear(_items, 0, _count);
		_count = 0;

		// Trim capacity back to minimum to free memory
		if (Capacity <= MINIMUM_ARRAY_SIZE) return;

		Resize(MINIMUM_ARRAY_SIZE);
	}

	public void CopyTo(T[] destination, int destinationIndex = 0)
	{
		if (destination is null) throw new ArgumentNullException(nameof(destination));
		if (destinationIndex < 0) throw new ArgumentOutOfRangeException(nameof(destinationIndex));
		if (destinationIndex + _count > destination.Length) throw new ArgumentException("Destination too small.");

		Array.Copy(_items, 0, destination, destinationIndex, _count);
	}

	public void EnsureCapacity(int min)
	{
		if (_items.Length >= min) return;

		int newCapacity                    = _items.Length == 0 ? MINIMUM_ARRAY_SIZE : _items.Length * 2;
		if (newCapacity < min) newCapacity = min;
		if (newCapacity > MAX_ARRAY_LENGTH)
		{
			if (min > MAX_ARRAY_LENGTH)
				throw new OutOfMemoryException("Required capacity exceeds maximum array length.");

			newCapacity = MAX_ARRAY_LENGTH;
		}

		Resize(newCapacity);
	}

	public void TrimExcess()
	{
		var threshold = (int)(_items.Length * 0.9);
		if (_items.Length <= MINIMUM_ARRAY_SIZE || _count >= threshold) return;

		int newSize = Math.Max(_count, MINIMUM_ARRAY_SIZE);
		Resize(newSize);
	}

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	/// <summary>
	///     Resizes the internal array to a new specified size, preserving existing elements.
	/// </summary>
	/// <param name="newSize">The new size of the array.</param>
	private void MaybeShrink()
	{
		if (_items.Length <= MINIMUM_ARRAY_SIZE || _count > _items.Length / 4) return;

		int newSize = Math.Max(_items.Length / 2, MINIMUM_ARRAY_SIZE);
		Resize(newSize);
	}

	private void Resize(int newSize)
	{
		var newItems = new T[newSize];
		Array.Copy(_items, newItems, _count);
		_items = newItems;
	}
}
