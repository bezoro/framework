using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.Core.Common.Interfaces;

namespace Bezoro.Core.Common.Extensions.Collections.Process;

/// <summary>
///     Contains methods for processing and formatting arrays.
/// </summary>
public static class ArrayProcess
{
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
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
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

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static async Task ProcessArrayAsync<T>(this T?[] array, Func<T, Task> action)
	{
		array.ThrowIfNull();

		for (var i = 0; i < array.Length; i++)
		{
			var t = array[i];
			if (t.IsNull()) continue;

			await action(t);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void ProcessArray<T>(this T?[] array, Action<T> action)
	{
		array.ThrowIfNull();

		for (var i = 0; i < array.Length; i++)
		{
			var t = array[i];
			if (t.IsNull()) continue;

			action(t);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static void ProcessArray<T>(this T?[] array) where T : IProcessable
	{
		if (array == null) throw new ArgumentNullException(nameof(array));

		foreach (var t in array)
		{
			if (t == null) continue;

			t.Process();
		}
	}
}
