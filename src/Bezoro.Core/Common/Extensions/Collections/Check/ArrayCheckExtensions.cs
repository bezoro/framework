namespace Bezoro.Core.Common.Extensions.Collections.Check;

/// <summary>
///     Contains methods for checking array state, properties and performing equality comparisons between arrays.
///     Provides functionality for both single-dimensional and two-dimensional arrays.
/// </summary>
public static class ArrayCheckExtensions
{
	/// <summary>
	///     Checks if two single-dimensional arrays have the same length and contain equal elements in the same order.
	/// </summary>
	/// <param name="array">The source array to compare.</param>
	/// <param name="b">The target array to compare against.</param>
	/// <returns>true if arrays have same length and contain equal elements in the same order; otherwise, false.</returns>
	public static bool AreEqual<T>(this T[] array, T[] b)
	{
		array.ThrowIfNull();
		b.ThrowIfNull();

		return array.Length == b.Length && array.SequenceEqual(b);
	}

	/// <summary>
	///     Checks if two two-dimensional arrays have the same dimensions and contain equal elements at corresponding
	///     positions.
	/// </summary>
	/// <param name="array2d">The source two-dimensional array to compare.</param>
	/// <param name="b">The target two-dimensional array to compare against.</param>
	/// <returns>true if arrays have same dimensions and contain equal elements at corresponding positions; otherwise, false.</returns>
	public static bool AreEqual<T>(this T[,] array2d, T[,] b)
	{
		array2d.ThrowIfNull();
		b.ThrowIfNull();

		if (ReferenceEquals(array2d, b)) return true;

		if (array2d.Length != b.Length) return false;
		if (array2d.GetLength(0) != b.GetLength(0) ||
			array2d.GetLength(1) != b.GetLength(1)) return false;

		int rows = array2d.GetLength(0);
		int cols = array2d.GetLength(1);

		var comparer = EqualityComparer<T>.Default;

		for (var i = 0; i < rows; i++)
		{
			for (var j = 0; j < cols; j++)
			{
				if (!comparer.Equals(array2d[i, j], b[i, j]))
					return false;
			}
		}

		return true;
	}

	/// <summary>
	///     Checks if a 2D array is null or empty.
	/// </summary>
	/// <typeparam name="T">The type of elements in the array.</typeparam>
	/// <param name="array2d">The 2D array to check.</param>
	/// <returns>True if the array is null or empty, false otherwise.</returns>
	public static bool IsNullOrEmpty<T>(this T[,]? array2d) =>
		array2d == null || array2d.Length == 0;
}
