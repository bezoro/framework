namespace Bezoro.Core.Common.Helpers;

/// <summary>
///     Provides methods for converting between different array shapes and representations.
/// </summary>
public static class ArrayConverter
{
	/// <summary>
	///     Converts a two-dimensional array to a one-dimensional array in row-major order.
	/// </summary>
	/// <typeparam name="T">The element type of the arrays.</typeparam>
	/// <param name="from">The source two-dimensional array to flatten.</param>
	/// <returns>
	///     A new one-dimensional array containing the elements of the source in row-major order.
	/// </returns>
	/// <exception cref="ArgumentNullException">
	///     Thrown if <paramref name="from" /> is <c>null</c>.
	/// </exception>
	public static T[] From2Dto1D<T>(T[,] from)
	{
		if (from == null) throw new ArgumentNullException(nameof(from));

		int rows   = from.GetLength(0);
		int cols   = from.GetLength(1);
		var result = new T[rows * cols];

		for (var i = 0; i < rows; i++)
		{
			for (var j = 0; j < cols; j++)
				result[i * cols + j] = from[i, j];
		}

		return result;
	}
}
