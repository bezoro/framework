using System.Collections;
using System.Runtime.CompilerServices;

namespace Bezoro.Core.Types;

/// <summary>
///     A zero-allocation enumerable that yields exactly one item.
/// </summary>
/// <typeparam name="T">The type of the item.</typeparam>
/// <remarks>
///     This struct avoids the heap allocation associated with iterator state machines
///     created by <c>yield return</c>. When enumerated via <c>foreach</c>, the JIT can
///     optimize away the enumerator allocation entirely.
/// </remarks>
public readonly struct SingleItemEnumerable<T> : IEnumerable<T>, IReadOnlyList<T>
{
	private readonly T _item;

	/// <summary>
	///     Initializes a new instance of the <see cref="SingleItemEnumerable{T}" /> struct.
	/// </summary>
	/// <param name="item">The single item to enumerate.</param>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public SingleItemEnumerable(T item)
	{
		_item = item;
	}

	/// <summary>
	///     Gets the number of elements in the collection (always 1).
	/// </summary>
	public int Count
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => 1;
	}

	/// <summary>
	///     Gets the element at the specified index.
	/// </summary>
	/// <param name="index">The zero-based index of the element to get.</param>
	/// <exception cref="IndexOutOfRangeException">Thrown if <paramref name="index" /> is not 0.</exception>
	public T this[int index]
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => index == 0 ? _item : throw new IndexOutOfRangeException();
	}

	/// <summary>
	///     Returns an enumerator that iterates through the single item.
	/// </summary>
	/// <returns>A struct enumerator for the single item.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public Enumerator GetEnumerator() => new(_item);

	/// <inheritdoc />
	IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

	/// <inheritdoc />
	IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();

	/// <summary>
	///     A struct enumerator for <see cref="SingleItemEnumerable{T}" />.
	/// </summary>
	public struct Enumerator : IEnumerator<T>
	{
		private readonly T    _item;
		private          bool _moved;

		/// <summary>
		///     Initializes a new instance of the <see cref="Enumerator" /> struct.
		/// </summary>
		/// <param name="item">The single item to enumerate.</param>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal Enumerator(T item)
		{
			_item  = item;
			_moved = false;
		}

		/// <inheritdoc />
		public T Current
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get => _item;
		}

		/// <inheritdoc />
		object? IEnumerator.Current => Current;

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool MoveNext()
		{
			if (_moved) return false;

			_moved = true;
			return true;
		}

		/// <inheritdoc />
		public void Dispose() { }

		/// <inheritdoc />
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void Reset() => _moved = false;
	}
}
