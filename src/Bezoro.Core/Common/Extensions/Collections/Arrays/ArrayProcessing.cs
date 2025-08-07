using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.Core.Common.Helpers;

namespace Bezoro.Core.Common.Extensions.Collections.Arrays;

/// <summary>
///     Contains methods for processing and formatting arrays.
/// </summary>
public static class ArrayProcessing
{
	/// <summary>
	///     Returns a string representation of the array, with each element name
	///     formatted according to the type of the element.
	/// </summary>
	public static string ToStringFormat<T>(this T[] array)
	{
		if (array == null) return "null";

		if (array.Length == 0) return "[]";

		return "[" + string.Join(", ", array.Select(ArrayHelper.GetElementName).ToString().Italic()) + "]";
	}

	/// <summary>
	///     Processes all elements of a 2D array in their order of appearance.
	/// </summary>
	/// <param name="array">The array to process.</param>
	/// <param name="processFunc">
	///     A function that takes an element of the array, its x- and y-coordinates, and a
	///     CancellationToken, and returns a UniTask.
	/// </param>
	/// <param name="cancellationToken">A CancellationToken that can be used to cancel the processing.</param>
	/// <returns>A UniTask that represents the asynchronous operation.</returns>
	public static async Task Process<T>(
		this T[,]                                  array,
		Func<T, int, int, CancellationToken, Task> processFunc,
		CancellationToken                          cancellationToken
	)
	{
		int rows = array.GetLength(0);
		int cols = array.GetLength(1);

		for (var y = 0; y < rows; y++)
		{
			for (var x = 0; x < cols; x++)
			{
				if (cancellationToken.IsCancellationRequested) return;

				if (array[y, x] == null) continue;

				await processFunc(array[y, x], x, y, cancellationToken);
				await Task.Yield();
			}
		}
	}
}
