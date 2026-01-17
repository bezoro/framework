namespace Bezoro.Core.Types;

/// <summary>
///     Encapsulates the outcome of searching for an element in an array.
/// </summary>
public readonly struct ArrayElementInfo<T>
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
	public ArrayElementInfo(uint? index, T? element, uint arrayLength)
	{
		if (index >= arrayLength)
			throw new ArgumentOutOfRangeException(
				nameof(index),
				index,
				$"Index must be less than array length: {arrayLength}.");

		Index       = arrayLength > 0 ? index : null;
		Element     = element;
		ArrayLength = arrayLength;
	}

	/// <summary>The compile-time element type (<c>typeof(T)</c>).</summary>
	public static Type ElementType => typeof(T);

	/// <summary>Indicates whether the search succeeded.</summary>
	public bool IsFound => Index.HasValue;

	/// <summary>The element that was searched for.</summary>
	public T? Element { get; }

	/// <summary>
	///     Runtime type of <see cref="Element" /> (falls back to <see cref="ElementType" /> if the element is
	///     <c>null</c>).
	///     Note: For <see cref="Nullable{T}" /> with a value, returns the underlying type due to boxing behavior.
	/// </summary>
	public Type RuntimeElementType => Element?.GetType() ?? ElementType;

	/// <summary>The total length of the array that was searched.</summary>
	public uint ArrayLength { get; }

	/// <summary>Index of <see cref="Element" /> in the array; <c>null</c> when not found.</summary>
	public uint? Index { get; }

	/// <summary>
	///     Returns an <see cref="ArrayElementInfo{T}" /> representing a failed search.
	/// </summary>
	public static ArrayElementInfo<T> NotFound(T searchedElement, uint arrayLength) =>
		new(null, searchedElement, arrayLength);

	/// <inheritdoc />
	public override string ToString() =>
		IsFound
			? $"ArrayElementInfo<{ElementType.Name}> {{ Index = {Index}, Element = {Element}, ArrayLength = {ArrayLength} }}"
			: $"ArrayElementInfo<{ElementType.Name}> {{ NotFound, Element = {(Element is null ? "<null>" : Element)}, ArrayLength = {ArrayLength} }}";

	/// <summary>Deconstructs the result into individual fields.</summary>
	public void Deconstruct(out uint? index, out T? element, out uint arrayLength)
	{
		index       = Index;
		element     = Element;
		arrayLength = ArrayLength;
	}
}
