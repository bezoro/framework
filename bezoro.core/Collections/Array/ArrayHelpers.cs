using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bezoro.Core.Logging;

namespace Bezoro.Core.Collections.Array
{
	public static class ArrayHelpers
	{
		public static int ParallelThreshold = 100;

		public static void Add<T>(
			ref T?[] array,
			T? element,
			out int index,
			int resizeFactor = 2 // Renamed parameter for consistency
		)
			where T : class
		{
			index = -1;

			if (element == null)
			{
				Logger.Log_Warning("Element is null. Add operation aborted.");
				return;
			}

			// Initialise the array if it's null or empty
			if (array.IsNullOrEmpty())
			{
				array        = new T[1];
				index        = 0;
				array[index] = element;

				Logger.LogSuccess(
					$"Array was null or empty. Initialized and added first element: {element} at index {index}.");

				return;
			}

			var nullIndex = FindNullIndex(array);

			if (nullIndex != -1)
			{
				index        = nullIndex; // Correctly assign the found null index to the out parameter
				array[index] = element;   // Use the correct index
				Logger.LogSuccess($"Added element: {element} into existing empty slot at index {index}.");
				return;
			}

			// No empty slot, resize the array
			ResizeByFactorAndAddElement(ref array, element, out index, resizeFactor);

			Logger.LogSuccess(
				$"Resized array by {resizeFactor} and added element: {element} at index {index}.");
		}

		public static void Add<T>(ref T?[] array, T? element, int resizeFactor = 2)
			where T : class
		{
			Add(ref array, element, out _, resizeFactor);
		}

		public static void Add_Unique<T>(ref T?[] array, T? element)
			where T : class
		{
			AddUnique(ref array, element, out _);
		}

		public static void AddUnique<T>(
			ref T?[] array,
			T? element,
			out int index,
			int resizeFactor = 2
		)
			where T : class
		{
			if (element == null)
			{
				index = -1;
				return;
			}

			if (array == null || array.Length == 0)
			{
				SetupArrayWithFirstElement(ref array, element);
				index = 0;
				return;
			}

			bool elementExists;

			if (array.Length > ParallelThreshold)
				elementExists = FindElementInParallel(array, element).RelevantIndex >= 0;
			else
				elementExists = FindElementSequentially(array, element).RelevantIndex >= 0;

			if (elementExists)
			{
				index = -1;
				Logger.Log_Warning($"Element {element} already exists in array. Add operation aborted.");
				return;
			}

			Add(ref array, element, out var i, resizeFactor);
			index = i;
			Logger.LogSuccess($"Adding unique element: {element}");
		}

		public static void Clear<T>(ref T[] array, ILogger logger = null)
		{
			if (array == null)
			{
				Logger.Log_Warning("Array is null. Aborting clear operation.");
				return;
			}

			if (array.Length == 0)
			{
				Logger.Log_Warning("Array is empty. Aborting clear operation.");
				return;
			}

			if (array.Length < ParallelThreshold)
				ClearSequential(ref array);
			else
				ClearParallel(array);

			Logger.LogSuccess("Cleared array.");
		}

		/// <summary>
		///     Clears all elements of the specified <paramref name="array" /> in parallel by assigning each element its default
		///     value.
		/// </summary>
		/// <param name="array">The array whose elements are to be cleared in parallel.</param>
		/// <typeparam name="T">The type of the elements in the array.</typeparam>
		public static void ClearParallel<T>(T[] array)
		{
			if (array.IsNullOrEmpty())
				return;

			Parallel.For(0, array.Length, i => array[i] = default);
		}

		/// <summary>
		///     Clears the contents of the provided array sequentially by resetting each element to its default value.
		/// </summary>
		/// <typeparam name="T">
		///     The type of elements contained in the array.
		/// </typeparam>
		/// <param name="array">
		///     A reference to the array to be cleared. Each element will be reset to its default value.
		/// </param>
		public static void ClearSequential<T>(ref T[] array)
		{
			if (array.IsNullOrEmpty()) return;

			System.Array.Clear(array, 0, array.Length);
		}

		public static bool CompareArrays<T>(T[]? a, T[]? b)
		{
			if (a == null && b == null) return true;
			if (a == null || b == null) return false;
			if (a.Length != b.Length) return false;

			for (var i = 0 ; i < a.Length ; i++)
			{
				if (EqualityComparer<T>.Default.Equals(a[i], b[i])) continue;

				Logger.Log_Info($"Array comparison failed at index {i}.");
				return false;
			}

			Logger.Log_Info("Arrays are equal");
			return true;
		}

		public static void Copy<T>(T[] from, ref T[] to, ILogger logger = null)
		{
			if (from == null)
			{
				Logger.Log_Warning("Source array is null. Aborting copy operation.");
				return;
			}

			if (from.Length == 0)
			{
				Logger.Log_Warning("Source array is empty. Aborting copy operation.");
				return;
			}

			if (to == null || to.Length < from.Length)
			{
				Logger.Log_Info($"Target array is null or too small. Resizing to {from.Length} elements.");
				to = new T[from.Length];
			}

			System.Array.Copy(from, to, from.Length);
			Logger.LogSuccess($"Copied {from.Length} elements from {from} to {to}.");
		}

		public static int Count_Non_Null_Elements<T>(T[] array)
			where T : class
		{
			if (array == null) return 0;

			var nonNullCount = 0;

			foreach (var element in array)
			{
				if (element != null)
					nonNullCount++;
			}

			Logger.Log_Info($"Counted {nonNullCount} non-null elements in the array.");
			return nonNullCount;
		}

		public static ArrayElementInfo<T> FindElementInParallel<T>(
			T[] array,
			T elementToFind
		)
		{
			if (array == null || array.Length == 0)
			{
				Logger.Log_Warning("Array is null or empty. Aborting search.");
				return new(-1, elementToFind, array?.Length ?? 0);
			}

			if (Equals(elementToFind, default(T)))
			{
				Logger.Log_Warning("Element to find is null. Aborting search.");
				return new(-1, default, array.Length);
			}

			var resultIndex = -1;
			var locker      = new object();

			Parallel.For(
				0, array.Length, (i, state) =>
				{
					if (array[i]?.Equals(elementToFind) != true) return;

					lock ( locker )
					{
						resultIndex = i;
						state.Stop();
					}
				}
			);

			if (resultIndex != -1)
				Logger.LogSuccess($"Element {elementToFind} found at index {resultIndex}.");
			else
				Logger.Log_Warning($"Element {elementToFind} not found in array.");

			return new(resultIndex, elementToFind, array.Length);
		}

		/// <summary>
		///     Sequentially searches for a specified element in an array and returns information about the search result.
		/// </summary>
		/// <typeparam name="T">The type of elements in the array.</typeparam>
		/// <param name="array">The array to search within.</param>
		/// <param name="elementToFind">The element to find in the array.</param>
		/// <returns>
		///     An <see cref="ArrayElementInfo{T}" /> containing:
		///     - The index of the found element (-1 if not found)
		///     - The element that was searched for
		///     - The length of the searched array
		/// </returns>
		/// <remarks>
		///     This method performs a sequential search through the array and logs the search result.
		///     If the array is null or empty, it returns immediately with appropriate information.
		/// </remarks>
		public static ArrayElementInfo<T> FindElementSequentially<T>(
			T[] array,
			T elementToFind
		)
		{
			if (array.IsNullOrEmpty())
			{
				Logger.Log_Warning("Array is null or empty. Aborting search.");
				return new(-1, elementToFind, array?.Length ?? 0);
			}

			for (var i = 0 ; i < array.Length ; i++)
			{
				if (!EqualityComparer<T>.Default.Equals(array[i], elementToFind)) continue;
				Logger.LogSuccess($"Element {elementToFind} found at index {i}");
				return new(i, elementToFind, array.Length);
			}

			Logger.Log_Warning($"Element {elementToFind} not found in array.");
			return new(-1, elementToFind, array.Length);
		}

		public static int FindNullIndex<T>(T[] array) where T : class?
		{
			if (array == null)
			{
				Logger.Log_Warning("Array is null. Aborting search.");
				return -1;
			}

			return array.Length > ParallelThreshold
				? FindElementInParallel(array, null).RelevantIndex
				: FindElementSequentially(array, null).RelevantIndex;
		}

		public static void InitializeNullArray<T>(ref T[] array)
		{
			if (array != null) return;

			array = System.Array.Empty<T>();
		}

		public static void Merge<T>(T[] source, ref T[] target, ILogger logger = null)
		{
			if (source == null)
			{
				Logger.Log_Warning("Source array is null. Aborting merge operation.");
				return;
			}

			if (source.Length == 0)
			{
				Logger.Log_Warning("Source array is empty. Aborting merge operation.");
				return;
			}

			if (target == null || target.Length == 0)
			{
				System.Array.Resize(ref target, source.Length);
				Copy(source, ref target);

				Logger.LogSuccess(
					$"Target array is null or empty. Resized to {source.Length} and copied source array."
				);

				return;
			}

			if (target.Length < source.Length)
			{
				System.Array.Resize(ref target, source.Length);
				Copy(source, ref target);

				Logger.LogSuccess(
					$"Target array is smaller than source array. Resized to {source.Length} and copied source array."
				);

				return;
			}

			for (var i = 0 ; i < source.Length ; i++)
			{
				if (!Equals(target[i], default(T))) continue;

				target[i] = source[i];
			}

			Logger.LogSuccess("Source array merged into target array.");
		}

		/// <summary>
		///     Merges the elements of the source array into the target array using the specified merging strategy and null element
		///     handling options.
		///     Depending on the strategy, the method can fill null slots, append, or prepend elements while managing null elements
		///     as per the configuration.
		/// </summary>
		/// <typeparam name="T">The type of elements in the arrays, constrained to reference type.</typeparam>
		/// <param name="source">The source array containing elements to be merged into the target array.</param>
		/// <param name="target">The target array that will be updated with elements from the source array.</param>
		/// <param name="strategy">
		///     Specifies how to merge the source array into the target array.
		///     Options include filling null elements, appending, prepending, or resizing the array.
		/// </param>
		/// <param name="elementInclusionStrategy">
		///     Determines how to handle null elements from the source during the merge process.
		///     Options include preserving null elements or excluding them from the merge.
		/// </param>
		public static void Merge<T>(
			T[] source,
			T[] target,
			MergingStrategy strategy = MergingStrategy.Fill_Null_Elements_And_Append,
			ElementInclusionStrategy elementInclusionStrategy = ElementInclusionStrategy.Exclude
		)
			where T : class
		{
			switch (strategy)
			{
				case MergingStrategy.Fill_Null_Elements_And_Append:
					Array.Merging.Fill_Null_Elements_And_Append(source, ref target, elementInclusionStrategy);
					break;
				case MergingStrategy.Fill_Null_Elements_And_Resize:
					Array.Merging.Fill_Null_Elements_And_Resize(source, ref target);
					break;
				case MergingStrategy.Fill_Null_Elements_And_Prepend:
					Array.Merging.Fill_Null_Elements_And_Prepend(source, ref target, elementInclusionStrategy);
					break;
				case MergingStrategy.Fill_Null_Elements_Only:
					Array.Merging.Fill_Null_Elements_Only(source, ref target);
					break;
				case MergingStrategy.Append_Only:
					Array.Merging.Append_Only(source, ref target, elementInclusionStrategy);
					break;
				case MergingStrategy.Prepend_Only:
					Array.Merging.Prepend_Only(source, ref target, elementInclusionStrategy);
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(strategy), strategy, null);
			}
		}

		public static bool Remove<T>(ref T[] array, T element, out int index, ILogger logger = null)
			where T : class
		{
			if (array == null)
			{
				Logger.Log_Warning("Array is null. Aborting remove operation.");
				index = -1;
				return false;
			}

			if (array.Length == 0)
			{
				Logger.Log_Warning("Array is empty. Aborting remove operation.");
				index = -1;
				return false;
			}

			if (element == null)
			{
				Logger.Log_Warning("Element is null. Aborting remove operation.");
				index = -1;
				return false;
			}

			var foundIndex = System.Array.IndexOf(array, element);

			if (foundIndex < 0)
			{
				index = -1;
				Logger.Log_Warning($"Element {element} not found in array. Aborting remove operation.");
				return false;
			}

			array[foundIndex] = null;
			index             = foundIndex;
			Logger.LogSuccess($"Removed element {element} at index {foundIndex}.");
			return true;
		}

		public static void RemoveDuplicates<T>(ref T[] array)
		{
			if (array.IsNullOrEmpty())
				return;

			if (array.Length <= 1)
				return;

			var uniqueElements = new List<T>();
			var seenElements   = new HashSet<T>();

			foreach (var element in array)
			{
				if (Equals(element, default(T))) continue;
				if (!seenElements.Add(element)) continue;
				uniqueElements.Add(element);
				Logger.Log_Info($"Removing duplicate: {element}");
			}

			array = uniqueElements.ToArray();
			Logger.LogSuccess("Removed duplicates from array.");
		}

		public static void RemoveElement<T>(
			ref T[] array,
			T element,
			ArrayRemovalApproach removalApproach = ArrayRemovalApproach.Resize
		)
		{
			// Validate input
			if (array == null || array.Length == 0)
			{
				Logger.Log_Warning("Array is null or empty. Aborting remove operation.");
				return;
			}

			if (Equals(element, default(T)))
			{
				Logger.Log_Warning("Element is null. Aborting remove operation.");
				return;
			}

			// Decide on the removal method based on the size of the array
			if (array.Length > ParallelThreshold)
			{
				Logger.Log_Info(
					$"Array size is above the parallel threshold ({ParallelThreshold}). Using parallel removal."
				);

				Remove_Element_Parallel(ref array, element, removalApproach);
			}
			else
			{
				Logger.Log_Info(
					$"Array size is below the parallel threshold ({ParallelThreshold}). Using sequential removal."
				);

				Remove_Element_Sequential(ref array, element, removalApproach);
			}
		}

		public static void ResizeByFactor<T_Array>(ref T_Array[] array, int factor)
		{
			if (factor <= 1)
			{
				Logger.Log_Warning($"Factor: {factor} is invalid. Array size will not be multiplied.");
				return;
			}

			if (array.IsNullOrEmpty())
			{
				Logger.Log_Warning("Array is null or empty. Array size will not be multiplied.");
				return;
			}

			System.Array.Resize(ref array, array.Length * factor);
			Logger.LogSuccess($"Array size resized by factor: {factor}");
		}

		public static bool SetupArrayWithFirstElement<T>(
			ref T[] array,
			T element
		)
		{
			if (array != null && array.Length != 0) return false;

			array    = new T[1];
			array[0] = element;
			Logger.Log_Info($"Created array and adding first element: {element}");
			return true;
		}

		private static void Remove_Element_Parallel<T>(
			ref T[] array,
			T element,
			ArrayRemovalApproach removalApproach
		)
		{
			var foundIndex = -1;
			var lockObj    = new object();
			var localArray = array;

			Parallel.For(
				0, localArray.Length, (i, state) =>
				{
					if (!EqualityComparer<T>.Default.Equals(localArray[i], element)) return;

					lock ( lockObj )
					{
						if (foundIndex != -1) return;

						foundIndex = i;
						state.Stop();
					}
				}
			);

			if (foundIndex == -1)
			{
				Logger.Log_Warning("Element not found in array. No operation performed.");
				return;
			}

			switch (removalApproach)
			{
				case ArrayRemovalApproach.Resize:
					Resize_Array_Parallel(ref array, foundIndex);
					break;

				case ArrayRemovalApproach.SetToNull:
					lock ( lockObj )
					{
						array[foundIndex] = default;
					}

					break;

				default:
					Logger.Log_Error($"Unhandled Array_Removal_Approach: {removalApproach}");
					break;
			}
		}

		private static void Remove_Element_Sequential<T>(
			ref T[] array,
			T element,
			ArrayRemovalApproach removalApproach
		)
		{
			// Find the index of the element
			var index = System.Array.IndexOf(array, element);

			if (index < 0)
			{
				Logger.Log_Warning("Element not found. No operation performed.");
				return;
			}

			// Handle removal approach
			switch (removalApproach)
			{
				case ArrayRemovalApproach.Resize:
					// Shift elements and resize the array
					for (var i = index ; i < array.Length - 1 ; i++)
						array[i] = array[i + 1];

					System.Array.Resize(ref array, array.Length - 1);

					break;
				case ArrayRemovalApproach.SetToNull:
					// Nullify the element
					array[index] = default;

					break;
				default:
					Logger.Log_Warning($"Unhandled Array_Removal_Approach: {removalApproach}");
					break;
			}
		}

		private static void Resize_Array_Parallel<T>(
			ref T[] array,
			int index
		)
		{
			// Create a new array with one less element
			var tempArray  = new T[array.Length - 1];
			var localArray = array;
			var copyLock   = new object();

			// Copy elements into the new array in parallel
			Parallel.For(
				0, localArray.Length, i =>
				{
					if (i < index)
						tempArray[i] = localArray[i];
					else if (i > index)
						tempArray[i - 1] = localArray[i];
				}
			);

			lock ( copyLock )
				array = tempArray;
		}

		private static void ResizeByFactorAndAddElement<T>(
			ref T[] array,
			T element,
			out int index,
			out int validSize,
			int resizeFactor
		)
			where T : class
		{
			ResizeByFactorAndAddElement(ref array, element, out index, resizeFactor);
			validSize = array.Length;
		}

		private static void ResizeByFactorAndAddElement<T>(
			ref T[] array,
			T element,
			out int index,
			int resizeFactor
		) where T : class
		{
			var oldLength = array.Length;
			ResizeByFactor(ref array, resizeFactor);
			array[oldLength] = element;
			index            = oldLength;
		}
	}
}
