using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
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
///     any removed element, avoiding the need to shift later elements. This makes
///     removal operations O(1) but does not preserve element ordering.
///     <para>
///         This class implements <see cref="IReadOnlyList{T}" /> for read-only access patterns,
///         while also providing mutation methods (Add, Remove, Clear, etc.) for modification.
///     </para>
/// </remarks>
[DebuggerDisplay("Count = {Count}, Capacity = {Capacity}")]
public sealed class SwapbackArray<T> : IReadOnlyList<T>
{
	private const uint MAX_ARRAY_LENGTH   = 0x7FFFFFC7; // match CLR array max length heuristic
	private const uint MINIMUM_ARRAY_SIZE = 4u;

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
	/// <param name="initialCapacity">The initial capacity of the array.</param>
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
	/// <param name="collection">
	///     The collection to initialize the array with.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///     Thrown when the collection is null.
	/// </exception>
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
	///     Initializes a new instance of the <see cref="SwapbackArray{T}" /> class.
	/// </summary>
	/// <param name="collection">
	///     The collection to initialize the array with.
	/// </param>
	/// <exception cref="ArgumentNullException">
	///     Thrown when the collection is null.
	/// </exception>
	public SwapbackArray(IEnumerable<T> collection)
	{
		switch (collection)
		{
			case null:
				throw new ArgumentNullException(nameof(collection));
			case ICollection<T> c:
			{
				var capacity = (uint)Math.Max(c.Count, MinimumArraySize);
				_items = new T[capacity];
				_count = (uint)c.Count;

				if (_count > 0)
					c.CopyTo(_items, 0);

				break;
			}
			default:
			{
				_items = new T[MinimumArraySize];
				_count = 0;
				foreach (var item in collection)
					Add(item);

				break;
			}
		}

		Version = 0;
	}

	/// <summary>
	///     Gets a value indicating whether the array is empty.
	/// </summary>
	/// <returns>true if the array is empty; otherwise, false.</returns>
	public bool IsEmpty => _count == 0;

	/// <summary>
	///     Gets a value indicating whether the array is full.
	/// </summary>
	/// <returns>true if the array is full; otherwise, false.</returns>
	public bool IsFull => _count == Capacity;

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
	public uint MaxArrayLength => MAX_ARRAY_LENGTH;

	/// <summary>
	///     The minimum capacity of the array.
	/// </summary>
	public uint MinimumArraySize => MINIMUM_ARRAY_SIZE;

	int IReadOnlyCollection<T>.Count => (int)_count;

	public uint ShrinkThresholdPercent { get; set; } = 25u;

	/// <summary>
	///     Version counter that increments on collection modifications. Used to detect
	///     concurrent modifications during enumeration to ensure collection consistency.
	/// </summary>
	public uint Version { get; private set; }

	/// <summary>
	///     Gets or sets the element at the specified index.
	/// </summary>
	/// <param name="index">The zero-based index of the element to get or set.</param>
	/// <returns>The element at the specified index.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	///     Thrown when the index is greater than or equal to <see cref="Count" />.
	/// </exception>
	public T this[uint index]
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get
		{
			if (index >= _count) throw new ArgumentOutOfRangeException(nameof(index));

			return _items[index];
		}
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		set
		{
			if (index >= _count) throw new ArgumentOutOfRangeException(nameof(index));

			_items[index] = value;
			Version++;
		}
	}

	T IReadOnlyList<T>.this[int index]
	{
		get
		{
			if (index < 0 || index >= (int)_count)
				throw new ArgumentOutOfRangeException(nameof(index));

			return this[(uint)index];
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Contains(T item)
	{
		if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
			return Array.IndexOf(_items, item, 0, (int)_count) >= 0;

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
	///     Tries to find the index of a specified item in the array.
	/// </summary>
	/// <param name="item">The item to find.</param>
	/// <param name="index">When the method returns, contains the index of the item if found; otherwise, null.</param>
	/// <returns>true if the item was found; otherwise, false.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool TryIndexOf(T item, out uint? index)
	{
		index = null;

		if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
		{
			int foundIndex = Array.IndexOf(_items, item, 0, (int)_count);
			if (foundIndex < 0) return false;

			index = (uint)foundIndex;
			return true;
		}

		var comparer = EqualityComparer<T>.Default;
		for (uint i = 0; i < _count; i++)
		{
			if (!comparer.Equals(_items[i], item)) continue;

			index = i;
			return true;
		}

		return false;
	}

	/// <summary>
	///     Attempts to retrieve the last element without removing it.
	/// </summary>
	/// <param name="value">
	///     When this method returns true, contains the last element; otherwise, the default value.
	/// </param>
	/// <returns>true if the array contains at least one element; otherwise, false.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool TryPeek(out T value)
	{
		if (_count == 0)
		{
			value = default!;
			return false;
		}

		value = _items[_count - 1];
		return true;
	}

	/// <summary>
	///     Removes and returns the last element, if any.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool TryPopBack(out T value)
	{
		if (_count == 0)
		{
			value = default!;
			return false;
		}

		_count--;
		value = _items[_count];
		if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
			_items[_count] = default!;

		Version++;
		MaybeShrink();
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
		if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
		{
			int index = Array.IndexOf(_items, item, 0, (int)_count);
			if (index < 0) return false;

			return TryRemoveAt((uint)index);
		}

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
		if (index >= _count) return false;

		_count--;

		if (index != _count)
			_items[index] = _items[_count];

		if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
			_items[_count] = default!;

		Version++;
		MaybeShrink();
		return true;
	}

	/// <summary>
	///     Returns a read-only collection view of this array.
	/// </summary>
	/// <returns>A read-only collection wrapper around this array.</returns>
	/// <remarks>
	///     The returned collection reflects the current state of the array.
	///     Changes to the underlying array will be visible through the read-only view.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public IReadOnlyCollection<T> AsReadOnlyCollection() => this;

	/// <summary>
	///     Returns a read-only span over the active elements of the array.
	/// </summary>
	/// <returns>A read-only span over the active elements.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public ReadOnlySpan<T> AsSpan() => new(_items, 0, (int)_count);

	/// <summary>
	///     Returns a writable span over the active elements. Mutations won't bump Version,
	///     but may cause enumeration inconsistencies if the collection is modified during iteration.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public Span<T> AsMutableSpanUnsafe() => new(_items, 0, (int)_count);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public SwapbackArrayEnumerator GetEnumerator() => new(this);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public T[] ToArray()
	{
		var result = new T[_count];
		Array.Copy(_items, 0, result, 0, (int)_count);
		return result;
	}

	/// <summary>
	///     Returns the index of the specified item in the array.
	/// </summary>
	/// <param name="item">The item to search for.</param>
	/// <returns>The zero-based index of the item if found.</returns>
	/// <exception cref="ArgumentException">
	///     Thrown when the item is not found in the array.
	/// </exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public uint IndexOf(T item)
	{
		if (_count == 0)
			throw new InvalidOperationException("Array is empty.");

		int index;
		if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
		{
			index = Array.IndexOf(_items, item, 0, (int)_count);
		}
		else
		{
			var comparer = EqualityComparer<T>.Default;
			index = -1;
			for (uint i = 0; i < _count; i++)
			{
				if (!comparer.Equals(_items[i], item)) continue;

				index = (int)i;
				break;
			}
		}

		if (index >= 0) return (uint)index;

		throw new ArgumentException("Item not found.");
	}

	/// <summary>
	///     Removes all elements that match the conditions defined by the specified predicate using swapback removal.
	/// </summary>
	/// <param name="match">The predicate that defines the conditions of the elements to remove.</param>
	/// <returns>The number of elements removed.</returns>
	/// <exception cref="ArgumentNullException">Thrown when match is null.</exception>
	/// <remarks>
	///     This method does not preserve element order and uses the swapback mechanism for O(1) removals.
	///     Time complexity: O(n) in the worst case, but actual number of swaps is minimized by bulk marking.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public uint RemoveAll(Predicate<T> match)
	{
		if (match is null) throw new ArgumentNullException(nameof(match));

		if (_count == 0) return 0;

		uint originalCount = _count;
		uint newCount      = CompactArray(match, originalCount);
		uint removedCount  = originalCount - newCount;

		if (removedCount > 0)
			FinalizeRemoval(newCount, originalCount);

		return removedCount;
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
	///     Adds the elements of the specified span to the end of the SwapbackArray.
	/// </summary>
	/// <param name="span">
	///     The span of elements to add to the SwapbackArray.
	/// </param>
	/// <remarks>
	///     This method copies the elements of the span to the end of the array.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void AddRange(ReadOnlySpan<T> span)
	{
		if (span.Length == 0) return;

		EnsureCapacity(_count + (uint)span.Length);
		span.CopyTo(new(_items, (int)_count, span.Length));
		_count += (uint)span.Length;
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
		if (collection is null)
			throw new ArgumentNullException(nameof(collection));

		bool added = collection switch
		{
			ICollection<T> c           => TryAddCollection(c),
			IReadOnlyCollection<T> roc => TryAddReadOnlyCollection(roc),
			_                          => AddEnumerable(collection)
		};

		if (added)
			Version++;
	}

	/// <summary>
	///     Adds an item without capacity checks. Caller must ensure capacity first.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void AddUnchecked(T item)
	{
		_items[_count++] = item;
		Version++;
	}

	/// <summary>
	///     Removes all elements from the array.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Clear(bool trim = true)
	{
		if (RuntimeHelpers.IsReferenceOrContainsReferences<T>() && _count > 0)
			Array.Clear(_items, 0, (int)_count);

		_count = 0;
		Version++;
		if (trim && Capacity > MinimumArraySize)
			Resize(MinimumArraySize);
	}

	/// <summary>
	///     Copies the elements of the array to a compatible one-dimensional span.
	/// </summary>
	/// <param name="destination">
	///     The span to which the elements of the current array will be copied.
	/// </param>
	/// <exception cref="ArgumentException">
	///     Thrown when the destination span is not large enough to accommodate the elements of the current array.
	/// </exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void CopyTo(Span<T> destination)
	{
		if (destination.Length < _count)
			throw new ArgumentException("Destination span is not large enough.");

		AsSpan().CopyTo(destination);
	}

	/// <summary>
	///     Copies the elements of the array to a compatible one-dimensional array.
	/// </summary>
	/// <param name="destination">
	///     The array to which the elements of the current array will be copied.
	/// </param>
	/// <param name="destinationIndex">
	///     The zero-based index in the destination array at which copying begins.
	/// </param>
	/// <exception cref="ArgumentNullException"></exception>
	/// <exception cref="ArgumentException"></exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void CopyTo(T[] destination, uint destinationIndex = 0)
	{
		if (destination is null) throw new ArgumentNullException(nameof(destination));

		if (destinationIndex > destination.Length ||
			_count > (uint)(destination.Length - (int)destinationIndex))
			throw new ArgumentException("Destination array is not large enough.");

		Array.Copy(_items, 0, destination, destinationIndex, (int)_count);
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
		var current = (uint)_items.Length;
		if (current >= min) return;

		uint newCapacity;
		if (current == 0)
			newCapacity = MinimumArraySize;
		else
			newCapacity = current > MAX_ARRAY_LENGTH / 2
							  ? MAX_ARRAY_LENGTH
							  : Math.Min(current * 2, MAX_ARRAY_LENGTH);

		if (newCapacity < min) newCapacity = min;

		if (newCapacity > MAX_ARRAY_LENGTH)
		{
			if (min > MAX_ARRAY_LENGTH)
				throw new OutOfMemoryException("Required capacity exceeds maximum array length.");

			newCapacity = MAX_ARRAY_LENGTH;
		}

		Resize(newCapacity);
	}

	/// <summary>
	///     Inserts an item at the specified index using swapback semantics.
	///     The last element in the array is moved to the insertion index, and the new item
	///     becomes the last element.
	/// </summary>
	/// <param name="index">The zero-based index at which to insert the item.</param>
	/// <param name="item">The item to insert.</param>
	/// <exception cref="ArgumentOutOfRangeException">
	///     Thrown when index is greater than <see cref="Count" />.
	/// </exception>
	/// <remarks>
	///     This operation maintains O(1) complexity by not shifting elements.
	///     If index equals Count, this is equivalent to Add(item).
	///     Otherwise, the current element at index is swapped to the end, and item takes its place.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void InsertAt(uint index, T item)
	{
		if (index > _count)
			throw new ArgumentOutOfRangeException(nameof(index), "Index cannot be greater than Count.");

		EnsureCapacity(_count + 1);

		if (index == _count)
		{
			_items[_count++] = item;
		}
		else
		{
			_items[_count] = _items[index];
			_items[index]  = item;
			_count++;
		}

		Version++;
	}

	/// <summary>
	///     Trims excess capacity when utilization is below the trim threshold.
	///     The array will only be trimmed if its utilization is below <see cref="minimumUtilizationThreshold" />.
	/// </summary>
	/// <param name="minimumUtilizationThreshold">
	///     The minimum required capacity utilization percentage before array size
	///     reduction is allowed.
	/// </param>
	/// <remarks>
	///     This method resizes the internal array to match the current count if utilization
	///     is below the threshold, but never below <see cref="MinimumArraySize" />.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void TrimExcess(uint minimumUtilizationThreshold = 90u)
	{
		var length = (uint)_items.Length;
		if (length <= MinimumArraySize) return;

		ulong left  = _count * 100UL;
		ulong right = length * minimumUtilizationThreshold;
		if (left >= right) return;

		uint newSize = Math.Max(_count, MinimumArraySize);
		Resize(newSize);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool AddEnumerable(IEnumerable<T> collection)
	{
		uint startCount = _count;
		foreach (var item in collection)
		{
			EnsureCapacity(_count + 1);
			_items[_count++] = item;
		}

		return _count != startCount;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool TryAddCollection(ICollection<T> collection)
	{
		if (collection.Count <= 0)
			return false;

		EnsureCapacity(_count + (uint)collection.Count);
		collection.CopyTo(_items, (int)_count);
		_count += (uint)collection.Count;
		return true;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool TryAddReadOnlyCollection(IReadOnlyCollection<T> collection)
	{
		if (collection.Count <= 0)
			return false;

		EnsureCapacity(_count + (uint)collection.Count);
		foreach (var item in collection)
			_items[_count++] = item;

		return true;
	}

	IEnumerator IEnumerable.      GetEnumerator() => new SwapbackArrayEnumerator(this);
	IEnumerator<T> IEnumerable<T>.GetEnumerator() => new SwapbackArrayEnumerator(this);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private uint CompactArray(Predicate<T> match, uint originalCount)
	{
		uint write = 0;
		uint read  = 0;

		while (read < originalCount)
		{
			if (!match(_items[read]))
			{
				if (write != read)
					_items[write] = _items[read];

				write++;
			}

			read++;
		}

		return write;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void ClearRemovedSlots(uint startIndex, uint endIndex)
	{
		if (!RuntimeHelpers.IsReferenceOrContainsReferences<T>())
			return;

		for (uint i = startIndex; i < endIndex; i++)
			_items[i] = default!;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void FinalizeRemoval(uint newCount, uint originalCount)
	{
		ClearRemovedSlots(newCount, originalCount);
		_count = newCount;
		Version++;
		MaybeShrink();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void MaybeShrink()
	{
		var length = (uint)_items.Length;
		if (length <= MinimumArraySize) return;

		ulong left  = _count * 100UL;
		ulong right = length * (ulong)ShrinkThresholdPercent;
		if (left > right) return;

		uint targetCapacity = Math.Max(_count * 2, MinimumArraySize);
		if (targetCapacity < length)
			Resize(targetCapacity);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void Resize(uint newSize)
	{
		if (newSize > MAX_ARRAY_LENGTH)
			throw new OutOfMemoryException("Array size exceeds maximum array length.");

		var newItems = new T[newSize];
		if (_count > 0)
			Array.Copy(_items, newItems, (int)_count);

		_items = newItems;
	}

	public struct SwapbackArrayEnumerator : IEnumerator<T>
	{
		private readonly SwapbackArray<T> _array;
		private readonly uint             _version;
		private          uint             _index;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal SwapbackArrayEnumerator(SwapbackArray<T> array)
		{
			_array   = array;
			_version = array.Version;
			_index   = 0;
			Current  = default!;
		}

		object IEnumerator.Current => Current!;

		public T Current { get; private set; }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool MoveNext()
		{
			if (_version != _array.Version)
				throw new InvalidOperationException("Collection was modified during enumeration.");

			if (_index < _array._count)
			{
				Current = _array._items[_index++];
				return true;
			}

			Current = default!;
			return false;
		}

		public void Dispose() { }

		public void Reset() => throw new NotSupportedException();
	}
}
