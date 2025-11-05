using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

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
	private const uint MAX_ARRAY_LENGTH         = 0x7FFFFFC7; // match CLR array max length heuristic
	private const uint MINIMUM_ARRAY_SIZE       = 4u;
	private const uint SHRINK_THRESHOLD_PERCENT = 25u;

	/// <summary>
	///     Internal storage array for the elements of the <see cref="SwapbackArray{T}" />.
	///     It dynamically resizes as elements are added or removed. The capacity is adjusted
	///     to optimize memory usage and access efficiency. This array supports reordering
	///     of elements when removing items.
	/// </summary>
	private T[] _items;

	/// <summary>
	///     Represents the current number of elements in the <see cref="SwapbackArray{T}" />.
	/// </summary>
	private uint _count;

	/// <summary>
	///     Initializes a new instance of the <see cref="SwapbackArray{T}" /> class.
	/// </summary>
	/// <param name="initialCapacity">The initial capacity of the array. Must be non-negative.</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when initialCapacity is negative.</exception>
	public SwapbackArray(uint initialCapacity = MINIMUM_ARRAY_SIZE)
	{
		uint capacity = Math.Max(initialCapacity, MinimumArraySize);
		_items  = new T[capacity];
		_count  = 0;
		Version = 0;
	}

	/// <summary>
	///     Initializes a new instance of the <see cref="SwapbackArray{T}" /> class.
	/// </summary>
	/// <param name="collection"></param>
	/// The collection to initialize the array with.
	/// <exception cref="ArgumentNullException"></exception>
	/// Thrown when collection is null.
	public SwapbackArray(ICollection<T> collection)
	{
		if (collection is null) throw new ArgumentNullException(nameof(collection));

		var capacity = (uint)Math.Max(collection.Count, MinimumArraySize);
		_items = new T[capacity];
		_count = (uint)collection.Count;

		if (_count > 0)
			collection.CopyTo(_items, 0);

		Version = 0;
	}

	/// <summary>
	///     Gets the total number of elements that the internal array can hold without resizing.
	/// </summary>
	public uint Capacity => (uint)_items.Length;

	/// <summary>
	///     Gets the number of elements currently contained in the <see cref="SwapbackArray{T}" />.
	/// </summary>
	public uint Count => _count;

	/// <summary>
	///     The maximum capacity of the array.
	/// </summary>
	public uint MaxCapacity => MAX_ARRAY_LENGTH;

	/// <summary>
	///     The minimum capacity of the array.
	/// </summary>
	public uint MinimumArraySize => MINIMUM_ARRAY_SIZE;

	/// <summary>
	///     The percentage of the array's capacity that must be occupied before it is trimmed.'
	/// </summary>
	public uint TrimThresholdPercent => 90;

	/// <summary>
	///     Version counter that increments on collection modifications. Used to detect
	///     concurrent modifications during enumeration to ensure collection consistency.
	/// </summary>
	public uint Version { get; private set; }

	public T this[uint index]
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => index >= _count ? throw new ArgumentOutOfRangeException(nameof(index)) : _items[index];
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		set
		{
			if (index >= _count) throw new ArgumentOutOfRangeException(nameof(index));

			_items[index] = value;
			Version++;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Contains(T item)
	{
		var comparer = EqualityComparer<T>.Default;
		for (uint i = 0; i < _count; i++)
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
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool TryGet(uint index, out T? value)
	{
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
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool TryRemove(T item)
	{
		var comparer = EqualityComparer<T>.Default;
		for (uint i = 0; i < _count; i++)
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
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool TryRemoveAt(uint index)
	{
		if (index >= _count)
			return false;

		// swap last into index and trim
		_count--;
		_items[index]  = _items[_count];
		_items[_count] = default!;

		Version++;
		MaybeShrink();
		return true;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public IEnumerator<T> GetEnumerator()
	{
		uint version = Version;
		uint count   = _count;
		for (uint i = 0; i < count; i++)
		{
			if (version != Version)
				throw new InvalidOperationException("Collection was modified during enumeration.");

			yield return _items[i];
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public ReadOnlySpan<T> AsSpan() => new(_items, 0, (int)_count);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public T[] ToArray()
	{
		var result = new T[_count];
		Array.Copy(_items, 0, result, 0, _count);
		return result;
	}

	/// <summary>
	///     Adds an item to the end of the SwapbackArray. If the array's capacity is not enough,
	///     its size is doubled to accommodate the new element.
	/// </summary>
	/// <param name="item">The item to add to the SwapbackArray.</param>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Add(T item)
	{
		EnsureCapacity(_count + 1);
		_items[_count++] = item;
		Version++;
	}

	/// <summary>
	///     Adds all elements from the specified collection to the end of this array.
	/// </summary>
	/// <param name="collection">The collection whose elements should be added.</param>
	/// <exception cref="ArgumentNullException">Thrown if collection is null.</exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void AddRange(IEnumerable<T> collection)
	{
		switch (collection)
		{
			case null:
				throw new ArgumentNullException(nameof(collection));
			case ICollection<T> c:
				if (c.Count > 0)
				{
					EnsureCapacity(_count + (uint)c.Count);
					c.CopyTo(_items, (int)_count);
					_count += (uint)c.Count;
					Version++;
				}

				break;
			default:
			{
				uint startCount = _count;
				foreach (var item in collection)
				{
					EnsureCapacity(_count + 1);
					_items[_count++] = item;
				}

				if (_count != startCount)
					Version++;

				break;
			}
		}
	}

	/// <summary>
	///     Removes all elements from the array.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Clear()
	{
		Array.Clear(_items, 0, (int)_count);
		_count = 0;
		Version++;

		// Trim capacity back to minimum to free memory
		if (Capacity <= MinimumArraySize) return;

		Resize(MinimumArraySize);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void CopyTo(T[] destination, uint destinationIndex = 0)
	{
		if (destination is null) throw new ArgumentNullException(nameof(destination));

		if (destinationIndex > destination.Length || _count > (uint)destination.Length - destinationIndex)
			throw new ArgumentException("Destination array is not large enough.");

		Array.Copy(_items, 0, destination, destinationIndex, _count);
	}

	/// <summary>
	///     Ensures the internal array has sufficient capacity to accommodate at least the specified number of elements.
	/// </summary>
	/// <param name="min">The minimum capacity required.</param>
	/// <exception cref="OutOfMemoryException">
	///     Thrown when the required capacity exceeds the maximum array length supported by the runtime.
	/// </exception>
	/// <remarks>
	///     Growth strategy:
	///     - For empty arrays, starts with minimum size (4)
	///     - For non-empty arrays, doubles the current capacity
	///     - If doubled capacity is not enough, uses requested minimum
	///     - Respects maximum array length limit (0x7FFFFFC7)
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void EnsureCapacity(uint min)
	{
		if (_items.Length >= min) return;

		uint newCapacity;

		if (_items.Length == 0)
		{
			newCapacity = MinimumArraySize;
		}
		else
		{
			if ((uint)_items.Length > MAX_ARRAY_LENGTH / 2)
				newCapacity = MAX_ARRAY_LENGTH;
			else
				newCapacity = (uint)_items.Length * 2;
		}

		if (newCapacity < min) newCapacity = min;

		if (newCapacity > MAX_ARRAY_LENGTH)
		{
			if (min > MAX_ARRAY_LENGTH)
				throw new OutOfMemoryException("Required capacity exceeds maximum array length.");

			newCapacity = MAX_ARRAY_LENGTH;
		}

		Resize(newCapacity);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void TrimExcess()
	{
		int threshold = _items.Length * (int)TrimThresholdPercent / 100;

		if (_items.Length <= MinimumArraySize || _count >= threshold) return;

		uint newSize = Math.Max(_count, MinimumArraySize);
		Resize(newSize);
	}

	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void MaybeShrink()
	{
		var length = (uint)_items.Length;
		if (length <= MinimumArraySize) return;

		float utilization = (float)_count / length;
		if (!(utilization <= (float)SHRINK_THRESHOLD_PERCENT / 100)) return;

		uint targetCapacity = Math.Max(_count * 2, MinimumArraySize);
		if (targetCapacity < length)
			Resize(targetCapacity);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void Resize(uint newSize)
	{
		var newItems = new T[newSize];
		Array.Copy(_items, newItems, _count);
		_items = newItems;
	}
}
