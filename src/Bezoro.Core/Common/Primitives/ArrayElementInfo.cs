using System;

namespace Bezoro.Core.Common.Primitives;

/// <summary>
///     Encapsulates the outcome of searching for an element in an array.
/// </summary>
public readonly struct ArrayElementInfo<T>
{
	/// <summary>
	///     Creates a new <see cref="ArrayElementInfo{T}" />.
	/// </summary>
	/// <param name="index">
	///     Index of the element inside the array or <c>-1</c> when the element was not found.
	/// </param>
	/// <param name="element">The element that was searched for.</param>
	/// <param name="arrayLength">Length of the searched array.</param>
	public ArrayElementInfo(int index, T element, int arrayLength)
	{
		Index       = index;
		Element     = element;
		ArrayLength = arrayLength;
	}


	/// <summary>The compile-time element type (<c>typeof(T)</c>).</summary>
	public static Type ArrayType => typeof(T);

	/// <summary>Indicates whether the search succeeded.</summary>
	public bool IsFound => Index >= 0;

	/// <summary>The total length of the array that was searched.</summary>
	public int ArrayLength { get; }

	/// <summary>Index of <see cref="Element" /> in the array; <c>-1</c> when not found.</summary>
	public int Index { get; }

	/// <summary>The element that was searched for.</summary>
	public T Element { get; }

	/// <summary>
	///     Runtime type of <see cref="Element" /> (falls back to <see cref="ArrayType" /> if the element is <c>null</c>).
	/// </summary>
	public Type ElementType => Element?.GetType() ?? ArrayType;

	/// <summary>
	///     Returns an <see cref="ArrayElementInfo{T}" /> representing a failed search.
	/// </summary>
	public static ArrayElementInfo<T> NotFound(T searchedElement, int arrayLength) =>
		new(-1, searchedElement, arrayLength);

	/// <inheritdoc />
	public override string ToString() =>
		$"[Index: {Index}, Element: {Element}, Length: {ArrayLength}, Type: {ArrayType.Name}]";

	/// <summary>Deconstructs the result into individual fields.</summary>
	public void Deconstruct(out int index, out T element, out int arrayLength)
	{
		index       = Index;
		element     = Element;
		arrayLength = ArrayLength;
	}
}
