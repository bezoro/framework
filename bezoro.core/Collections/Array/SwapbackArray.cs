using System;
using Bezoro.Core.Logging;

namespace Bezoro.Core.Collections.Array
{
	/// <summary>
	///     Provides a dynamic array that allows for efficient addition of elements and removal
	///     by swapping the last element into the vacancy caused by the removal action. Suitable
	///     for use cases where order preservation of elements is not required.
	/// </summary>
	/// <typeparam name="T">Specifies the type of elements stored in the array.</typeparam>
	public class SwapbackArray<T>
	{
		private const int _MINIMUM_ARRAY_SIZE = 4;

		public SwapbackArray(int initialCapacity = _MINIMUM_ARRAY_SIZE, ILogger logger = null)
		{
			_items  = new T[Math.Max(initialCapacity, _MINIMUM_ARRAY_SIZE)];
			_count  = 0;
			_logger = logger;

			Logger.LogSuccess($"SwapbackArray initialized with capacity: {initialCapacity}");
		}

		/// <summary>
		///     A logger interface instance used to record operational messages, errors, and warnings
		///     within the <see cref="SwapbackArray{T}" />. It enables tracking and debugging of array-related
		///     operations and processes, enhancing observability and diagnostics.
		/// </summary>
		private ILogger _logger;

		/// <summary>
		///     Represents the current number of elements in the <see cref="SwapbackArray{T}" />.
		/// </summary>
		private int _count;

		private readonly object _lock = new();

		/// <summary>
		///     Internal storage array for the elements of the <see cref="SwapbackArray{T}" />.
		///     It dynamically resizes as elements are added or removed. The capacity is adjusted
		///     to optimize memory usage and access efficiency. This array supports reordering
		///     of elements when removing items.
		/// </summary>
		private T[] _items;

		/// <summary>
		///     Gets the total number of elements that the internal array can hold without resizing.
		/// </summary>
		public int Capacity => _items.Length;

		/// <summary>
		///     Gets the number of elements currently contained in the <see cref="SwapbackArray{T}" />.
		/// </summary>
		public int Count => _count;

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
		public bool Try_Get(int index, out T value)
		{
			if (index < 0 || index >= _count)
			{
				Logger.Log_Error($"Attempted to access invalid index: {index}");
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
			Logger.LogInfo("Clearing all elements from the array.");
			System.Array.Clear(_items, 0, _count);
			_count = 0;
			Logger.LogSuccess("Array cleared.");
		}

		public void Remove_At(int index)
		{
			lock ( _lock ) // Ensure atomicity
			{
				if (!Validate_Remove_Index(index))
					return;

				Perform_Swap_And_Trim(index);

				if (Should_Resize(_count, _items.Length))
				{
					Logger.LogWarning("Array under-utilized. Resizing...");
					Resize(Math.Max(_items.Length / 2, _MINIMUM_ARRAY_SIZE));
					Logger.LogSuccess($"Array resized to new capacity: {_items.Length}");
				}
			}
		}

		private bool Should_Resize(int currentCount, int currentCapacity) =>
			currentCount <= currentCapacity / 4 && currentCapacity > _MINIMUM_ARRAY_SIZE;

		private bool Validate_Remove_Index(int index)
		{
			if (_items.IsNullOrEmpty())
			{
				Logger.Log_Error("Remove operation failed. Array is empty.");
				return false;
			}

			if (index < 0 || index >= _count)
			{
				Logger.Log_Exception(
					new ArgumentOutOfRangeException(
						nameof(index), index, $"Remove operation failed. Invalid index. Aborting {nameof(Remove_At)}.")
				);

				return false;
			}

			return true;
		}

		private void Perform_Swap_And_Trim(int index)
		{
			Logger.LogInfo($"Removing item at index {index}.");

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
			System.Array.Copy(_items, newItems, _count);
			_items = newItems;

			Logger.LogSuccess($"Resize operation complete. New size: {newSize}");
		}

		~SwapbackArray()
		{
			Logger.LogSuccess("SwapbackArray finalized and resources released.");
			_items  = null;
			_logger = null;
		}
	}
}
