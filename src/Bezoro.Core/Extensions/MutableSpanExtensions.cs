namespace Bezoro.Core.Extensions;

/// <summary>Compacts caller-owned spans without resizing their backing storage.</summary>
public static class MutableSpanExtensions
{
	/// <summary>Removes an element by shifting its successors left, then clears the old last element.</summary>
	/// <returns>The borrowed span with its logical length reduced by one. Retain this return value.</returns>
	/// <remarks>O(Length - index). Aliases observe the mutation; the backing array length is unchanged.</remarks>
	/// <exception cref="ArgumentOutOfRangeException">The index is outside the logical span.</exception>
	public static Span<T> RemoveAtOrdered<T>(this Span<T> span, int index)
	{
		if (index < 0 || index >= span.Length) throw new ArgumentOutOfRangeException(nameof(index));
		span.Slice(index + 1).CopyTo(span.Slice(index));
		span[span.Length - 1] = default!;
		return span.Slice(0, span.Length - 1);
	}

	/// <summary>Removes an element by moving the last element into its position, then clears the old last element.</summary>
	/// <returns>The borrowed span with its logical length reduced by one. Retain this return value.</returns>
	/// <remarks>O(1). Element order may change. Aliases observe the mutation; backing storage is not resized.</remarks>
	/// <exception cref="ArgumentOutOfRangeException">The index is outside the logical span.</exception>
	public static Span<T> RemoveAtSwapBack<T>(this Span<T> span, int index)
	{
		if (index < 0 || index >= span.Length) throw new ArgumentOutOfRangeException(nameof(index));
		int last = span.Length - 1;
		span[index] = span[last];
		span[last] = default!;
		return span.Slice(0, last);
	}
}
