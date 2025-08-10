using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Bezoro.Core.Common.Extensions.Collections.Arrays;

/// <summary>
///     Contains methods for checking array state and properties.
/// </summary>
public static class ArrayChecks
{
	/// <summary>
	///     Checks if two arrays are equal.
	/// </summary>
	/// <param name="array">The first array to compare.</param>
	/// <param name="b">The second array to compare.</param>
	/// <returns>true if the arrays are equal, false otherwise.</returns>
	public static bool ArraysAreEqual<T>(this T[] array, T[] b)
	{
		array.ThrowIfNull();
		b.ThrowIfNull();

		return array.Length == b.Length && array.SequenceEqual(b);
	}

	public static bool ArraysAreEqual<T>(this T[,] array2d, T[,] b)
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
	///     Returns <c>true</c> when the array is non-null and has <c>Length == 0</c>.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsEmpty<T>(this T[] array)
	{
		array.ThrowIfNull();
		return array.Length == 0;
	}

	public static bool IsEmpty<T>(this T[,] array2d)
	{
		array2d.ThrowIfNull();
		return array2d.Length == 0;
	}

	/// <summary>
	///     Returns <c>true</c> when the array reference is <c>null</c>.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsNull<T>([NotNullWhen(false)] this T[]? array) => array is null;

	public static bool IsNull<T>([NotNullWhen(false)] this T[,]? array2d) => array2d is null;

	/// <summary>
	///     Combined convenience check—mirrors <see cref="string.IsNullOrEmpty(string)" />.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static bool IsNullOrEmpty<T>([NotNullWhen(false)] this T[]? array) => array is null || array.Length == 0;


	/// <summary>
	///     Checks if a 2D array is null or empty.
	/// </summary>
	/// <typeparam name="T">The type of elements in the array.</typeparam>
	/// <param name="array2d">The 2D array to check.</param>
	/// <returns>True if the array is null or empty, false otherwise.</returns>
	public static bool IsNullOrEmpty<T>(this T[,]? array2d) =>
		array2d == null || array2d.Length == 0;
}
