namespace Bezoro.Core.Extensions;

/// <summary>Finds insertion bounds in already sorted spans without allocating.</summary>
public static class SortedSpanExtensions
{
	/// <summary>Finds the first index whose value compares greater than or equal to the search value.</summary>
	/// <remarks>The span must be sorted with the supplied comparer. Returns Length when no such value exists.</remarks>
	public static int LowerBound<T>(this ReadOnlySpan<T> span, T value, IComparer<T>? comparer = null)
	{
		comparer ??= Comparer<T>.Default;
		int low = 0;
		int high = span.Length;
		while (low < high)
		{
			int middle = low + ((high - low) >> 1);
			if (comparer.Compare(span[middle], value) < 0) low = middle + 1;
			else high = middle;
		}
		return low;
	}

	/// <summary>Finds the first index whose value compares strictly greater than the search value.</summary>
	/// <remarks>The span must be sorted with the supplied comparer. Returns Length when no such value exists.</remarks>
	public static int UpperBound<T>(this ReadOnlySpan<T> span, T value, IComparer<T>? comparer = null)
	{
		comparer ??= Comparer<T>.Default;
		int low = 0;
		int high = span.Length;
		while (low < high)
		{
			int middle = low + ((high - low) >> 1);
			if (comparer.Compare(span[middle], value) <= 0) low = middle + 1;
			else high = middle;
		}
		return low;
	}

	/// <summary>Finds the lower insertion bound in an already sorted mutable span without changing it.</summary>
	public static int LowerBound<T>(this Span<T> span, T value, IComparer<T>? comparer = null) =>
		((ReadOnlySpan<T>)span).LowerBound(value, comparer);

	/// <summary>Finds the upper insertion bound in an already sorted mutable span without changing it.</summary>
	public static int UpperBound<T>(this Span<T> span, T value, IComparer<T>? comparer = null) =>
		((ReadOnlySpan<T>)span).UpperBound(value, comparer);
}
