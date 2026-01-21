using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Bezoro.Core.Types;

/// <summary>
///     Encapsulates the outcome of searching for an element in an array.
/// </summary>
[DebuggerDisplay("Index={Index}, Found={IsFound}, ArrayLength={ArrayLength}")]
public readonly struct ArrayElementInfo<T> : IEquatable<ArrayElementInfo<T>>
{
	/// <summary>
	///     Creates a new <see cref="ArrayElementInfo{T}" />.
	/// </summary>
	/// <param name="index">
	///     Index of the element inside the array or <c>null</c> when the element was not found.
	/// </param>
	/// <param name="element">The element that was searched for.</param>
	/// <param name="arrayLength">Length of the searched array.</param>
	/// <exception cref="ArgumentOutOfRangeException"> Thrown when index is greater than arrayLength. </exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public ArrayElementInfo(uint? index, T? element, uint arrayLength)
	{
		if (index >= arrayLength)
			ThrowIndexOutOfRange(index, arrayLength);

		Index       = arrayLength > 0 ? index : null;
		Element     = element;
		ArrayLength = arrayLength;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void ThrowIndexOutOfRange(uint? index, uint arrayLength) =>
		throw new ArgumentOutOfRangeException(
			nameof(index),
			index,
			$"Index must be less than array length: {arrayLength}.");

	/// <summary>The compile-time element type (<c>typeof(T)</c>).</summary>
	public static Type ElementType
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => typeof(T);
	}

	/// <summary>Indicates whether the search succeeded.</summary>
	public bool IsFound
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => Index.HasValue;
	}

	/// <summary>The element that was searched for.</summary>
	public T? Element { get; }

	/// <summary>
	///     Runtime type of <see cref="Element" /> (falls back to <see cref="ElementType" /> if the element is
	///     <c>null</c>).
	///     Note: For <see cref="Nullable{T}" /> with a value, returns the underlying type due to boxing behavior.
	/// </summary>
	public Type RuntimeElementType
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get => Element?.GetType() ?? ElementType;
	}

	/// <summary>The total length of the array that was searched.</summary>
	public uint ArrayLength { get; }

	/// <summary>Index of <see cref="Element" /> in the array; <c>null</c> when not found.</summary>
	public uint? Index { get; }

	/// <summary>
	///     Returns an <see cref="ArrayElementInfo{T}" /> representing a failed search.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ArrayElementInfo<T> NotFound(T searchedElement, uint arrayLength) =>
		new(null, searchedElement, arrayLength);

	/// <inheritdoc />
	public override string ToString() =>
		IsFound
			? $"ArrayElementInfo<{ElementType.Name}> {{ Index = {Index}, Element = {Element}, ArrayLength = {ArrayLength} }}"
			: $"ArrayElementInfo<{ElementType.Name}> {{ NotFound, Element = {(Element is null ? "<null>" : Element)}, ArrayLength = {ArrayLength} }}";

	/// <summary>Deconstructs the result into individual fields.</summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Deconstruct(out uint? index, out T? element, out uint arrayLength)
	{
		index       = Index;
		element     = Element;
		arrayLength = ArrayLength;
	}

	/// <summary>
	///     Indicates whether the current <see cref="ArrayElementInfo{T}" /> is equal to another.
	/// </summary>
	/// <param name="other">An <see cref="ArrayElementInfo{T}" /> to compare with this instance.</param>
	/// <returns><c>true</c> if equal; otherwise, <c>false</c>.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Equals(ArrayElementInfo<T> other) =>
		Index == other.Index &&
		ArrayLength == other.ArrayLength &&
		EqualityComparer<T?>.Default.Equals(Element, other.Element);

	/// <inheritdoc />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override bool Equals(object? obj) => obj is ArrayElementInfo<T> other && Equals(other);

	/// <inheritdoc />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public override int GetHashCode() => HashCode.Combine(Index, ArrayLength, Element);

	/// <summary>
	///     Indicates whether two <see cref="ArrayElementInfo{T}" /> values are equal.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator ==(ArrayElementInfo<T> left, ArrayElementInfo<T> right) => left.Equals(right);

	/// <summary>
	///     Indicates whether two <see cref="ArrayElementInfo{T}" /> values are not equal.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator !=(ArrayElementInfo<T> left, ArrayElementInfo<T> right) => !left.Equals(right);
}
