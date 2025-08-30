using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.Core.Common.Interfaces;

namespace Bezoro.Core.Common.Extensions.Collections.Process;

/// <summary>
///     Contains extension methods for processing and manipulating arrays, providing both synchronous
///     and asynchronous operations for one-dimensional and two-dimensional arrays.
/// </summary>
public static class ArrayProcess
{
	/// <summary>
	///     Processes all non-null elements of a 2D array in their order of appearance.
	/// </summary>
	/// <param name="array">The array to process.</param>
	/// <param name="processFunc">
	///     A function that takes an element of the array, its x- and y-coordinates, and a
	///     CancellationToken, and returns a Task. Null elements are skipped during processing.
	/// </param>
	/// <param name="cancellationToken">A CancellationToken that can be used to cancel the processing.</param>
	/// <returns>A Task that represents the asynchronous operation.</returns>
	/// <remarks>
	///     The processing can be cancelled either via the cancellation token or when an OperationCanceledException occurs.
	///     After processing each element, the method yields control back to the caller.
	/// </remarks>
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

				try
				{
					await processFunc(array[y, x], x, y, cancellationToken);
				}
				catch (OperationCanceledException)
				{
					return;
				}

				await Task.Yield();
			}
		}
	}

	/// <summary>
	///     Asynchronously processes all non-null elements in a one-dimensional array.
	/// </summary>
	/// <typeparam name="T">The type of elements in the array.</typeparam>
	/// <param name="array">The array to process.</param>
	/// <param name="action">The asynchronous action to perform on each non-null element.</param>
	/// <returns>A Task representing the asynchronous operation.</returns>
	/// <exception cref="ArgumentNullException">Thrown when the array is null.</exception>
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

	/// <summary>
	///     Synchronously processes all non-null elements in a one-dimensional array.
	/// </summary>
	/// <typeparam name="T">The type of elements in the array.</typeparam>
	/// <param name="array">The array to process.</param>
	/// <param name="action">The action to perform on each non-null element.</param>
	/// <exception cref="ArgumentNullException">Thrown when the array is null.</exception>
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

	/// <summary>
	///     Processes all non-null elements in a one-dimensional array that implement IProcessable.
	/// </summary>
	/// <typeparam name="T">The type of elements in the array, must implement IProcessable.</typeparam>
	/// <param name="array">The array containing IProcessable elements to process.</param>
	/// <exception cref="ArgumentNullException">Thrown when the array is null.</exception>
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
