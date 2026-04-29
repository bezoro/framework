using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Bezoro.Core.Types;

/// <summary>
///     Encapsulates the outcome of searching for an element in an array.
/// </summary>
/// <typeparam name="T">The type of elements in the array.</typeparam>
/// <remarks>
///     <para>
///         This is a lightweight, immutable struct that captures the result of an array search operation,
///         including whether the element was found, its index, and the array's length.
///     </para>
///     <para>
///         Thread Safety: This type is immutable and therefore thread-safe.
///     </para>
/// </remarks>
/// <example>
///     <code>
///     // Creating a found result
///     var found = ArrayElementInfo&lt;string&gt;.Found(2, "hello", 5);
///     if (found) // implicit bool conversion
///     {
///         Console.WriteLine($"Found at index {(uint?)found}"); // explicit uint? conversion
///     }
///
///     // Creating a not-found result
///     var notFound = ArrayElementInfo&lt;string&gt;.NotFound("missing", 5);
///
///     // Using TryGetElement pattern
///     if (found.TryGetElement(out var element, out var index))
///     {
///         Console.WriteLine($"Element '{element}' at index {index}");
///     }
///
///     // Using deconstruction
///     var (idx, elem, len) = found;
///     </code>
/// </example>
[DebuggerDisplay("Index={Index}, Found={IsFound}, ArrayLength={ArrayLength}")]
public readonly struct ArrayElementInfo<T> : IEquatable<ArrayElementInfo<T>>
#if NET6_0_OR_GREATER
	,
	ISpanFormattable
#endif
{
	/// <summary>
	///     Creates a new <see cref="ArrayElementInfo{T}" />.
	///     Use <see cref="Found" /> or <see cref="NotFound" /> factory methods instead.
	/// </summary>
	/// <param name="index">
	///     Index of the element inside the array or <c>null</c> when the element was not found.
	/// </param>
	/// <param name="element">The element that was searched for.</param>
	/// <param name="arrayLength">Length of the searched array.</param>
	/// <exception cref="ArgumentOutOfRangeException"> Thrown when index is greater than arrayLength. </exception>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal ArrayElementInfo(uint? index, T? element, uint arrayLength)
	{
		if (index >= arrayLength)
			ThrowIndexOutOfRange(index, arrayLength);

		Index       = arrayLength > 0 ? index : null;
		Element     = element;
		ArrayLength = arrayLength;
	}

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
	///     Indicates whether two <see cref="ArrayElementInfo{T}" /> values are equal.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator ==(ArrayElementInfo<T> left, ArrayElementInfo<T> right) => left.Equals(right);

	/// <summary>
	///     Explicitly converts the <see cref="ArrayElementInfo{T}" /> to a nullable <see cref="uint" /> representing the
	///     index.
	/// </summary>
	/// <param name="info">The <see cref="ArrayElementInfo{T}" /> to convert.</param>
	/// <returns>The index if found; otherwise, <c>null</c>.</returns>
	/// <example>
	///     <code>
	///     var info = ArrayElementInfo&lt;string&gt;.Found(2, "hello", 5);
	///     uint? index = (uint?)info; // explicit conversion to uint?
	///     </code>
	/// </example>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static explicit operator uint?(ArrayElementInfo<T> info) => info.Index;

	/// <summary>
	///     Implicitly converts the <see cref="ArrayElementInfo{T}" /> to a <see cref="bool" /> indicating whether the element
	///     was found.
	/// </summary>
	/// <param name="info">The <see cref="ArrayElementInfo{T}" /> to convert.</param>
	/// <returns><c>true</c> if the element was found; otherwise, <c>false</c>.</returns>
	/// <example>
	///     <code>
	///     var info = ArrayElementInfo&lt;string&gt;.Found(0, "hello", 5);
	///     if (info) // implicit conversion to bool
	///     {
	///         Console.WriteLine("Found!");
	///     }
	///     </code>
	/// </example>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static implicit operator bool(ArrayElementInfo<T> info) => info.IsFound;

	/// <summary>
	///     Indicates whether two <see cref="ArrayElementInfo{T}" /> values are not equal.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool operator !=(ArrayElementInfo<T> left, ArrayElementInfo<T> right) => !left.Equals(right);

	#region Equality

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

	#endregion

	/// <summary>
	///     Returns an <see cref="ArrayElementInfo{T}" /> representing a successful search.
	/// </summary>
	/// <param name="index">The index where the element was found.</param>
	/// <param name="element">The element that was found.</param>
	/// <param name="arrayLength">The length of the array that was searched.</param>
	/// <returns>A new <see cref="ArrayElementInfo{T}" /> with <see cref="IsFound" /> set to <c>true</c>.</returns>
	/// <exception cref="ArgumentOutOfRangeException">
	///     Thrown when <paramref name="index" /> is greater than or equal to
	///     <paramref name="arrayLength" />.
	/// </exception>
	/// <example>
	///     <code>
	///     var found = ArrayElementInfo&lt;string&gt;.Found(2, "hello", 10);
	///     // found.IsFound == true
	///     // found.Index == 2
	///     // found.Element == "hello"
	///     </code>
	/// </example>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ArrayElementInfo<T> Found(uint index, T element, uint arrayLength) =>
		new(index, element, arrayLength);

	/// <summary>
	///     Returns an <see cref="ArrayElementInfo{T}" /> representing a failed search.
	/// </summary>
	/// <param name="searchedElement">The element that was searched for.</param>
	/// <param name="arrayLength">The length of the array that was searched.</param>
	/// <returns>A new <see cref="ArrayElementInfo{T}" /> with <see cref="IsFound" /> set to <c>false</c>.</returns>
	/// <example>
	///     <code>
	///     var notFound = ArrayElementInfo&lt;string&gt;.NotFound("missing", 10);
	///     // notFound.IsFound == false
	///     // notFound.Element == "missing"
	///     </code>
	/// </example>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static ArrayElementInfo<T> NotFound(T? searchedElement, uint arrayLength) =>
		new(null, searchedElement, arrayLength);

	/// <inheritdoc />
	public override string ToString() =>
		IsFound
			? $"ArrayElementInfo<{ElementType.Name}> {{ Index = {Index}, Element = {Element}, ArrayLength = {ArrayLength} }}"
			: $"ArrayElementInfo<{ElementType.Name}> {{ NotFound, Element = {(Element is null ? "<null>" : Element)}, ArrayLength = {ArrayLength} }}";

	/// <summary>
	///     Attempts to retrieve the element and its index.
	/// </summary>
	/// <param name="element">When this method returns <c>true</c>, contains the found element; otherwise, the default value.</param>
	/// <param name="index">When this method returns <c>true</c>, contains the index; otherwise, 0.</param>
	/// <returns><c>true</c> if the element was found; otherwise, <c>false</c>.</returns>
	/// <example>
	///     <code>
	///     var info = ArrayElementInfo&lt;string&gt;.Found(2, "hello", 5);
	///     if (info.TryGetElement(out var element, out var index))
	///     {
	///         Console.WriteLine($"Found '{element}' at index {index}");
	///     }
	///     </code>
	/// </example>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool TryGetElement([MaybeNullWhen(false)] out T element, out uint index)
	{
		if (IsFound)
		{
			element = Element!;
			index   = Index!.Value;
			return true;
		}

		element = default;
		index   = 0;
		return false;
	}

	/// <summary>
	///     Returns the element if found, or the default value of <typeparamref name="T" /> if not found.
	/// </summary>
	/// <returns>The element if found; otherwise, <c>default(T)</c>.</returns>
	/// <example>
	///     <code>
	///     var found = ArrayElementInfo&lt;int&gt;.Found(0, 42, 5);
	///     int value = found.GetElementOrDefault(); // returns 42
	///
	///     var notFound = ArrayElementInfo&lt;int&gt;.NotFound(42, 5);
	///     int missing = notFound.GetElementOrDefault(); // returns 0
	///     </code>
	/// </example>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public T? GetElementOrDefault() => IsFound ? Element : default;

	/// <summary>
	///     Returns the element if found, or the specified default value if not found.
	/// </summary>
	/// <param name="defaultValue">The value to return if the element was not found.</param>
	/// <returns>The element if found; otherwise, <paramref name="defaultValue" />.</returns>
	/// <example>
	///     <code>
	///     var notFound = ArrayElementInfo&lt;string&gt;.NotFound(null, 5);
	///     string value = notFound.GetElementOrDefault("fallback"); // returns "fallback"
	///     </code>
	/// </example>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public T? GetElementOrDefault(T defaultValue) => IsFound ? Element : defaultValue;

	/// <summary>Deconstructs the result into individual fields.</summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Deconstruct(out uint? index, out T? element, out uint arrayLength)
	{
		index       = Index;
		element     = Element;
		arrayLength = ArrayLength;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void ThrowIndexOutOfRange(uint? index, uint arrayLength) =>
		throw new ArgumentOutOfRangeException(
			nameof(index),
			index,
			$"Index must be less than array length: {arrayLength}."
		);

#if NET6_0_OR_GREATER
	/// <summary>
	///     Tries to format the value into the provided span of characters.
	/// </summary>
	/// <param name="destination">The span in which to write the formatted value.</param>
	/// <param name="charsWritten">When this method returns, contains the number of characters written.</param>
	/// <param name="format">A span containing the characters that represent a standard or custom format string (unused).</param>
	/// <param name="provider">An object that supplies culture-specific formatting information (unused).</param>
	/// <returns><c>true</c> if the formatting was successful; otherwise, <c>false</c>.</returns>
	public bool TryFormat(
		Span<char>         destination,
		out int            charsWritten,
		ReadOnlySpan<char> format,
		IFormatProvider?   provider)
	{
		charsWritten = 0;

		string typeName = ElementType.Name;

		if (IsFound)
		{
			// Format: "ArrayElementInfo<TypeName> { Index = X, Element = Y, ArrayLength = Z }"
			if (!TryWriteString(destination, ref charsWritten, "ArrayElementInfo<"))
				return false;

			if (!TryWriteString(destination, ref charsWritten, typeName))
				return false;

			if (!TryWriteString(destination, ref charsWritten, "> { Index = "))
				return false;

			if (!Index!.Value.TryFormat(destination[charsWritten..], out int indexWritten))
				return false;

			charsWritten += indexWritten;
			if (!TryWriteString(destination, ref charsWritten, ", Element = "))
				return false;

			if (!TryWriteString(destination, ref charsWritten, Element?.ToString() ?? "<null>"))
				return false;

			if (!TryWriteString(destination, ref charsWritten, ", ArrayLength = "))
				return false;

			if (!ArrayLength.TryFormat(destination[charsWritten..], out int lengthWritten))
				return false;

			charsWritten += lengthWritten;
			if (!TryWriteString(destination, ref charsWritten, " }"))
				return false;
		}
		else
		{
			// Format: "ArrayElementInfo<TypeName> { NotFound, Element = Y, ArrayLength = Z }"
			if (!TryWriteString(destination, ref charsWritten, "ArrayElementInfo<"))
				return false;

			if (!TryWriteString(destination, ref charsWritten, typeName))
				return false;

			if (!TryWriteString(destination, ref charsWritten, "> { NotFound, Element = "))
				return false;

			if (!TryWriteString(
					destination, ref charsWritten, Element is null ? "<null>" : Element.ToString() ?? "<null>"
				))
				return false;

			if (!TryWriteString(destination, ref charsWritten, ", ArrayLength = "))
				return false;

			if (!ArrayLength.TryFormat(destination[charsWritten..], out int lengthWritten))
				return false;

			charsWritten += lengthWritten;
			if (!TryWriteString(destination, ref charsWritten, " }"))
				return false;
		}

		return true;
	}

	private static bool TryWriteString(Span<char> destination, ref int charsWritten, string value)
	{
		if (destination.Length - charsWritten < value.Length)
			return false;

		value.AsSpan().CopyTo(destination[charsWritten..]);
		charsWritten += value.Length;
		return true;
	}

	/// <summary>
	///     Formats the value using the specified format and format provider.
	/// </summary>
	/// <param name="format">The format to use (unused).</param>
	/// <param name="formatProvider">The provider to use to format the value (unused).</param>
	/// <returns>The formatted string representation.</returns>
	public string ToString(string? format, IFormatProvider? formatProvider) => ToString();

#endif
}
