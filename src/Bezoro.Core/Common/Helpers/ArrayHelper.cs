using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bezoro.Core.Common.Extensions;
using Bezoro.Core.Common.Primitives;

namespace Bezoro.Core.Common.Helpers
{
	public static class ArrayHelper
	{
		public static int ParallelThreshold { get; private set; } = 100;

		public static ArrayElementInfo<T> FindElementInParallel<T>(
			T[] array,
			T elementToFind
		)
		{
			if (array == null || array.Length == 0)
			{
				Logger.LogWarning("Array is null or empty. Aborting search.");
				return new ArrayElementInfo<T>(-1, elementToFind, array?.Length ?? 0);
			}

			if (Equals(elementToFind, default(T)))
			{
				Logger.LogWarning("Element to find is null. Aborting search.");
				return new ArrayElementInfo<T>(-1, default, array.Length);
			}

			int resultIndex = -1;
			var locker      = new object();

			Parallel.For(
				0, array.Length, (i, state) =>
				{
					if (array[i]?.Equals(elementToFind) != true)
					{
						return;
					}

					lock ( locker )
					{
						resultIndex = i;
						state.Stop();
					}
				}
			);

			if (resultIndex != -1)
			{
				Logger.LogSuccess($"Element {elementToFind} found at index {resultIndex}.");
			}
			else
			{
				Logger.LogWarning($"Element {elementToFind} not found in array.");
			}

			return new ArrayElementInfo<T>(resultIndex, elementToFind, array.Length);
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
				Logger.LogWarning("Array is null or empty. Aborting search.");
				return new ArrayElementInfo<T>(-1, elementToFind, array?.Length ?? 0);
			}

			for (var i = 0 ; i < array.Length ; i++)
			{
				if (!EqualityComparer<T>.Default.Equals(array[i], elementToFind))
				{
					continue;
				}

				Logger.LogSuccess($"Element {elementToFind} found at index {i}");
				return new ArrayElementInfo<T>(i, elementToFind, array.Length);
			}

			Logger.LogWarning($"Element {elementToFind} not found in array.");
			return new ArrayElementInfo<T>(-1, elementToFind, array.Length);
		}

		public static bool CompareArrays<T>(T[]? a, T[]? b)
		{
			if (a == null && b == null)
			{
				return true;
			}

			if (a == null || b == null)
			{
				return false;
			}

			if (a.Length != b.Length)
			{
				return false;
			}

			for (var i = 0 ; i < a.Length ; i++)
			{
				if (EqualityComparer<T>.Default.Equals(a[i], b[i]))
				{
					continue;
				}

				Logger.LogInfo($"Array comparison failed at index {i}.", nameof(ArrayHelper), LogCategory.Utilities);
				return false;
			}

			Logger.LogInfo("Arrays are equal", nameof(ArrayHelper), LogCategory.Utilities);
			return true;
		}

		public static bool Remove<T>(ref T[] array, T element, out int index)
			where T : class
		{
			if (array == null)
			{
				Logger.LogWarning("Array is null. Aborting remove operation.");
				index = -1;
				return false;
			}

			if (array.Length == 0)
			{
				Logger.LogWarning("Array is empty. Aborting remove operation.");
				index = -1;
				return false;
			}

			if (element == null)
			{
				Logger.LogWarning("Element is null. Aborting remove operation.");
				index = -1;
				return false;
			}

			int foundIndex = Array.IndexOf(array, element);

			if (foundIndex < 0)
			{
				index = -1;
				Logger.LogWarning($"Element {element} not found in array. Aborting remove operation.");
				return false;
			}

			array[foundIndex] = null;
			index             = foundIndex;
			Logger.LogSuccess($"Removed element {element} at index {foundIndex}.");
			return true;
		}

		public static bool SetupArrayWithFirstElement<T>(
			ref T[] array,
			T element
		)
		{
			if (array != null && array.Length != 0)
			{
				return false;
			}

			array    = new T[1];
			array[0] = element;
			Logger.LogInfo($"Created array and adding first element: {element}", nameof(ArrayHelper),
				LogCategory.Utilities);
			return true;
		}

		public static int CountNonNullElements<T>(T[] array)
			where T : class
		{
			if (array == null)
			{
				return 0;
			}

			var nonNullCount = 0;

			foreach (var element in array)
			{
				if (element != null)
				{
					nonNullCount++;
				}
			}

			Logger.LogInfo($"Counted {nonNullCount} non-null elements in the array.", nameof(ArrayHelper),
				LogCategory.Utilities);
			return nonNullCount;
		}

		public static int FindNullIndex<T>(T[] array) where T : class?
		{
			if (array == null)
			{
				Logger.LogWarning("Array is null. Aborting search.");
				return -1;
			}

			return array.Length > ParallelThreshold
				? FindElementInParallel(array, null).RelevantIndex
				: FindElementSequentially(array, null).RelevantIndex;
		}

		public static void Add<T>(
			ref T?[] array,
			T? element,
			out int index,
			int resizeFactor = 2
		)
			where T : class
		{
			index = -1;

			if (element == null)
			{
				Logger.LogWarning("Element is null. Add operation aborted.");
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

			int nullIndex = FindNullIndex(array);

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
			where T : class =>
			Add(ref array, element, out _, resizeFactor);

		public static void Add_Unique<T>(ref T?[] array, T? element)
			where T : class =>
			AddUnique(ref array, element, out _);

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
			{
				elementExists = FindElementInParallel(array, element).RelevantIndex >= 0;
			}
			else
			{
				elementExists = FindElementSequentially(array, element).RelevantIndex >= 0;
			}

			if (elementExists)
			{
				index = -1;
				Logger.LogWarning($"Element {element} already exists in array. Add operation aborted.");
				return;
			}

			Add(ref array, element, out int i, resizeFactor);
			index = i;
			Logger.LogSuccess($"Adding unique element: {element}");
		}

		public static void Clear<T>(ref T[] array)
		{
			if (array == null)
			{
				Logger.LogWarning("Array is null. Aborting clear operation.");
				return;
			}

			if (array.Length == 0)
			{
				Logger.LogWarning("Array is empty. Aborting clear operation.");
				return;
			}

			if (array.Length < ParallelThreshold)
			{
				ClearSequential(ref array);
			}
			else
			{
				ClearParallel(array);
			}

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
			{
				return;
			}

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
			if (array.IsNullOrEmpty())
			{
				return;
			}

			Array.Clear(array, 0, array.Length);
		}

		public static void Copy<T>(T[] from, ref T[] to)
		{
			if (from == null)
			{
				Logger.LogWarning("Source array is null. Aborting copy operation.");
				return;
			}

			if (from.Length == 0)
			{
				Logger.LogWarning("Source array is empty. Aborting copy operation.");
				return;
			}

			if (to == null || to.Length < from.Length)
			{
				Logger.LogInfo($"Target array is null or too small. Resizing to {from.Length} elements.",
					nameof(ArrayHelper), LogCategory.Utilities);
				to = new T[from.Length];
			}

			Array.Copy(from, to, from.Length);
			Logger.LogSuccess($"Copied {from.Length} elements from {from} to {to}.");
		}

		public static void InitializeNullArray<T>(ref T[] array)
		{
			if (array != null)
			{
				return;
			}

			array = Array.Empty<T>();
		}

		public static void Merge<T>(T[] source, ref T[] target)
		{
			if (source == null)
			{
				Logger.LogWarning("Source array is null. Aborting merge operation.");
				return;
			}

			if (source.Length == 0)
			{
				Logger.LogWarning("Source array is empty. Aborting merge operation.");
				return;
			}

			if (target == null || target.Length == 0)
			{
				Array.Resize(ref target, source.Length);
				Copy(source, ref target);

				Logger.LogSuccess(
					$"Target array is null or empty. Resized to {source.Length} and copied source array."
				);

				return;
			}

			if (target.Length < source.Length)
			{
				Array.Resize(ref target, source.Length);
				Copy(source, ref target);

				Logger.LogSuccess(
					$"Target array is smaller than source array. Resized to {source.Length} and copied source array."
				);

				return;
			}

			for (var i = 0 ; i < source.Length ; i++)
			{
				if (!Equals(target[i], default(T)))
				{
					continue;
				}

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
					Merging.FillNullElementsAndAppend(source, ref target, elementInclusionStrategy);
					break;
				case MergingStrategy.Fill_Null_Elements_And_Resize:
					Merging.FillNullElementsAndResize(source, ref target);
					break;
				case MergingStrategy.Fill_Null_Elements_And_Prepend:
					Merging.FillNullElementsAndPrepend(source, ref target, elementInclusionStrategy);
					break;
				case MergingStrategy.Fill_Null_Elements_Only:
					Merging.FillNullElementsOnly(source, ref target);
					break;
				case MergingStrategy.Append_Only:
					Merging.AppendOnly(source, ref target, elementInclusionStrategy);
					break;
				case MergingStrategy.Prepend_Only:
					Merging.PrependOnly(source, ref target, elementInclusionStrategy);
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(strategy), strategy, null);
			}
		}

		public static void RemoveDuplicates<T>(ref T[] array)
		{
			if (array.IsNullOrEmpty())
			{
				return;
			}

			if (array.Length <= 1)
			{
				return;
			}

			var uniqueElements = new List<T>();
			var seenElements   = new HashSet<T>();

			foreach (T element in array)
			{
				if (Equals(element, default(T)))
				{
					continue;
				}

				if (!seenElements.Add(element))
				{
					continue;
				}

				uniqueElements.Add(element);
				Logger.LogInfo($"Removing duplicate: {element}", nameof(ArrayHelper), LogCategory.Utilities);
			}

			array = uniqueElements.ToArray();
			Logger.LogSuccess("Removed duplicates from array.");
		}

		public static void RemoveElement<T>(
			ref T[] array,
			T element,
			Enums removalApproach = Enums.Resize
		)
		{
			// Validate input
			if (array == null || array.Length == 0)
			{
				Logger.LogWarning("Array is null or empty. Aborting remove operation.");
				return;
			}

			if (Equals(element, default(T)))
			{
				Logger.LogWarning("Element is null. Aborting remove operation.");
				return;
			}

			// Decide on the removal method based on the size of the array
			if (array.Length > ParallelThreshold)
			{
				Logger.LogInfo(
					$"Array size is above the parallel threshold ({ParallelThreshold}). Using parallel removal.",
					nameof(ArrayHelper), LogCategory.Utilities);

				Remove_Element_Parallel(ref array, element, removalApproach);
			}
			else
			{
				Logger.LogInfo(
					$"Array size is below the parallel threshold ({ParallelThreshold}). Using sequential removal.",
					nameof(ArrayHelper), LogCategory.Utilities);

				RemoveElementSequential(ref array, element, removalApproach);
			}
		}

		public static void ResizeByFactor<T_Array>(ref T_Array[] array, int factor)
		{
			if (factor <= 1)
			{
				Logger.LogWarning($"Factor: {factor} is invalid. Array size will not be multiplied.");
				return;
			}

			if (array.IsNullOrEmpty())
			{
				Logger.LogWarning("Array is null or empty. Array size will not be multiplied.");
				return;
			}

			Array.Resize(ref array, array.Length * factor);
			Logger.LogSuccess($"Array size resized by factor: {factor}");
		}

		public static void SetParallelThreshold(int threshold) =>
			ParallelThreshold = threshold;

		private static void Remove_Element_Parallel<T>(
			ref T[] array,
			T element,
			Enums removalApproach
		)
		{
			int foundIndex = -1;
			var lockObj    = new object();
			T[] localArray = array;

			Parallel.For(
				0, localArray.Length, (i, state) =>
				{
					if (!EqualityComparer<T>.Default.Equals(localArray[i], element))
					{
						return;
					}

					lock ( lockObj )
					{
						if (foundIndex != -1)
						{
							return;
						}

						foundIndex = i;
						state.Stop();
					}
				}
			);

			if (foundIndex == -1)
			{
				Logger.LogWarning("Element not found in array. No operation performed.");
				return;
			}

			switch (removalApproach)
			{
				case Enums.Resize:
					ResizeArrayParallel(ref array, foundIndex);
					break;

				case Enums.SetToNull:
					lock ( lockObj )
					{
						array[foundIndex] = default;
					}

					break;

				default:
					Logger.LogError($"Unhandled Array_Removal_Approach: {removalApproach}");
					break;
			}
		}

		private static void RemoveElementSequential<T>(
			ref T[] array,
			T element,
			Enums removalApproach
		)
		{
			// Find the index of the element
			int index = Array.IndexOf(array, element);

			if (index < 0)
			{
				Logger.LogWarning("Element not found. No operation performed.");
				return;
			}

			// Handle removal approach
			switch (removalApproach)
			{
				case Enums.Resize:
					// Shift elements and resize the array
					for (int i = index ; i < array.Length - 1 ; i++)
						array[i] = array[i + 1];

					Array.Resize(ref array, array.Length - 1);

					break;
				case Enums.SetToNull:
					// Nullify the element
					array[index] = default;

					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(removalApproach), removalApproach, null);
			}
		}

		private static void ResizeArrayParallel<T>(
			ref T[] array,
			int index
		)
		{
			// Create a new array with one less element
			var tempArray  = new T[array.Length - 1];
			T[] localArray = array;
			var copyLock   = new object();

			// Copy elements into the new array in parallel
			Parallel.For(
				0, localArray.Length, i =>
				{
					if (i < index)
					{
						tempArray[i] = localArray[i];
					}
					else if (i > index)
					{
						tempArray[i - 1] = localArray[i];
					}
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
			int oldLength = array.Length;
			ResizeByFactor(ref array, resizeFactor);
			array[oldLength] = element;
			index            = oldLength;
		}
	}

	/// <summary>
	///     The <c>Merging</c> class provides various static methods to manipulate and merge arrays.
	///     These methods handle operations such as filling, appending, and prepending elements between arrays,
	///     with additional options for handling null elements and resizing arrays.
	/// </summary>
	public static class Merging
	{
		/// <summary>
		///     Appends elements from the source array to the end of the target array.
		///     Optionally, null elements in the source array can be excluded during the append operation.
		///     This method does not alter the existing elements in the target array.
		/// </summary>
		/// <typeparam name="T">The type of elements in the arrays.</typeparam>
		/// <param name="source">The source array providing elements to append to the target array.</param>
		/// <param name="target">The target array to which elements will be appended. This may be resized if necessary.</param>
		/// <param name="elementInclusionStrategy">
		///     A boolean flag indicating whether to include null elements from the source array in the append operation.
		/// </param>
		public static void AppendOnly<T>(
			T[] source,
			ref T[] target,
			ElementInclusionStrategy elementInclusionStrategy = ElementInclusionStrategy.Exclude
		) where T : class
		{
			// Validate input arrays. Log warnings if null and exit early if validation fails.
			if (!ValidateSource(source))
			{
				return;
			}

			var elementsToAppendCount = 0;

			// Calculate how many valid elements will be appended from the source array.
			foreach (var element in source)
			{
				if (elementInclusionStrategy == ElementInclusionStrategy.Include || element != null)
				{
					elementsToAppendCount++;
				}
			}

			// If there are no valid elements to append, exit early.
			if (elementsToAppendCount == 0)
			{
				Logger.LogInfo("No elements to append from the source array.", nameof(ArrayHelper),
					LogCategory.Utilities);
				return;
			}

			// Resize the target array to hold the new elements.
			int originalLength = target.Length;
			Array.Resize(ref target, originalLength + elementsToAppendCount);

			// Append elements directly to the target array.
			int appendIndex = originalLength;

			foreach (var element in source)
			{
				if (elementInclusionStrategy == ElementInclusionStrategy.Include || element != null)
				{
					target[appendIndex++] = element;
				}
			}

			Logger.LogSuccess($"Appended {elementsToAppendCount} elements to the end of the target array.");
		}

		/// <summary>
		///     Fills the null elements in the target array with elements from the source array and
		///     appends the remaining unconsumed elements from the source array to the end of the target array.
		///     Optionally, null elements in the source array can be excluded from the operation.
		/// </summary>
		/// <typeparam name="T">The type of elements in the arrays.</typeparam>
		/// <param name="source">The source array providing elements to fill the null slots in the target array.</param>
		/// <param name="target">
		///     The target array whose null elements will be replaced, and where remaining source elements will be
		///     appended.
		/// </param>
		/// <param name="elementInclusionStrategy">
		///     A boolean flag indicating whether to include null elements from the source array during the operation.
		/// </param>
		public static void FillNullElementsAndAppend<T>(
			T[] source,
			ref T[] target,
			ElementInclusionStrategy elementInclusionStrategy = ElementInclusionStrategy.Exclude
		) where T : class
		{
			// Validate the input arrays. Logs warnings and exit early if validation fails.
			if (source == null || target == null)
			{
				return;
			}

			// Cache the condition to skip null elements in the source
			bool excludeNulls      = elementInclusionStrategy == ElementInclusionStrategy.Exclude;
			bool targetInitialized = HandleNullTargetInitialization(source, ref target, excludeNulls);

			if (targetInitialized)
			{
				return;
			}

			var sourceIndex = 0;

			// Step 1: Fill null elements in target from the source array.
			sourceIndex = PopulateTargetWithSource(source, target, sourceIndex, excludeNulls);

			// Step 2: Calculate remaining valid source elements.
			if (CheckForRemainingElements(source, sourceIndex, excludeNulls, out int appendCount))
			{
				return;
			}

			// Resize target and append remaining valid source elements.
			AppendValidElements(source, ref target, appendCount, sourceIndex, excludeNulls);
		}

		public static void FillNullElementsAndPrepend<T>(
			T[] source,
			ref T[] target,
			ElementInclusionStrategy elementInclusionStrategy = ElementInclusionStrategy.Exclude
		) where T : class
		{
			// Validate input arrays. Log warnings if null and exit early if validation fails.
			if (!ValidateSource(source))
			{
				return;
			}

			// Step 1: Fill null elements in the target array using the source array.
			var sourceIndex = 0;

			for (var targetIndex = 0 ;
				 targetIndex < target.Length && sourceIndex < source.Length ;
				 targetIndex++)
			{
				if (target[targetIndex] == null &&
					(source[sourceIndex] != null || elementInclusionStrategy == ElementInclusionStrategy.Include))
				{
					target[targetIndex] = source[sourceIndex];
				}

				if (elementInclusionStrategy == ElementInclusionStrategy.Exclude || source[sourceIndex] != null)
				{
					sourceIndex++;
				}
			}

			// Step 2: Prepend remaining source elements directly to the target array.
			int remainingLength = source.Length - sourceIndex;

			if (remainingLength > 0)
			{
				// Optimization: Allocate new target array once directly for final size.
				var newTarget = new T[target.Length + remainingLength];

				// Copy remaining source elements to the beginning of the new target array.
				Array.Copy(source, sourceIndex, newTarget, 0, remainingLength);

				// Copy the original target elements into the new array.
				Array.Copy(target, 0, newTarget, remainingLength, target.Length);

				// Assign the new array back to the target reference.
				target = newTarget;
			}

			// Log success message.
			Logger.LogSuccess("Filled null slots in the target array and prepended remaining source elements.");
		}

		/// <summary>
		///     Fills the null elements in the target array with elements from the source array.
		///     Resizes the target array if there are insufficient null elements to accommodate all source elements.
		/// </summary>
		/// <typeparam name="T">The type of elements in the arrays.</typeparam>
		/// <param name="source">The source array providing elements to fill the null slots in the target array.</param>
		/// <param name="target">The target array whose null elements will be replaced and potentially resized.</param>
		public static void FillNullElementsAndResize<T>(T[] source, ref T[] target)
			where T : class
		{
			// Validate input arrays. Log warnings if null and exit early if validation fails.
			if (!ValidateSource(source))
			{
				return;
			}

			// Keep track of source and target array indices.
			var sourceIndex = 0;
			var targetIndex = 0;

			// Fill null elements in the target array using the source array.
			while (targetIndex < target.Length && sourceIndex < source.Length)
			{
				if (target[targetIndex] == null)
				{
					target[targetIndex] = source[sourceIndex];
					sourceIndex++;
				}

				targetIndex++;
			}

			// If there are remaining elements in the source array, resize the target array.
			if (sourceIndex < source.Length)
			{
				// Add remaining elements to a resized array.
				int totalNewSize  = target.Length + (source.Length - sourceIndex);
				var resizedTarget = new T[totalNewSize];

				// Use Array.Copy for better memory and performance efficiency.
				Array.Copy(target, resizedTarget, target.Length);

				Array.Copy(
					source, sourceIndex, resizedTarget, target.Length, source.Length - sourceIndex
				);

				// Reassign the reference and log success.
				target = resizedTarget;

				Logger.LogSuccess($"Target array resized to fit all source elements. New size: {totalNewSize}");
			}
			else
			{
				// Log success if no resizing was needed.
				Logger.LogSuccess("Merge Completed. No resizing needed.");
			}
		}

		/// <summary>
		///     Replaces null elements in the target array with corresponding non-null elements from the source array.
		///     This method does not resize the target array; only existing null slots are filled, and any remaining source
		///     elements are ignored if they do not fit.
		/// </summary>
		/// <typeparam name="T">The type of elements in the array.</typeparam>
		/// <param name="source">The source array providing elements to fill the null slots in the target array.</param>
		/// <param name="target">The target array whose null elements will be replaced.</param>
		public static void FillNullElementsOnly<T>(T[] source, ref T[] target)
			where T : class
		{
			// Validate input arrays. Log warnings if null and exit early if validation fails.
			if (!ValidateSource(source))
			{
				return;
			}

			// If the target array is null, initialize it with the source array and log the operation.
			if (target == null || target.Length == 0)
			{
				target = source.ToArray(); // Clone the source array to avoid reference issues.
				Logger.LogSuccess("Target array was null and has been initialized with the source array.");

				return;
			}

			// Track the index of the source array.
			var sourceIndex = 0;

			// Iterate through the target array.
			for (var targetIndex = 0 ; targetIndex < target.Length ; targetIndex++)
			{
				// Skip non-null elements in the target.
				if (target[targetIndex] != null)
				{
					continue;
				}

				// Exit early if there are no more source elements.
				if (sourceIndex >= source.Length)
				{
					break;
				}

				// Get the next element from the source array.
				T? sourceElement = source[sourceIndex];

				if (sourceElement != null)
				{
					// Fill the null slot in the target array.
					target[targetIndex] = sourceElement;
					Logger.LogInfo($"Element {sourceElement} added to target array at index {targetIndex}.",
						nameof(ArrayHelper), LogCategory.Utilities);
				}

				// Move to the next element in the source array.
				sourceIndex++;
			}

			// Log warning if not all source elements could be used.
			if (sourceIndex < source.Length)
			{
				Logger.LogWarning(
					"Source array contains additional elements that could not be used to fill target array null slots.");

				return;
			}

			// Log success message.
			Logger.LogSuccess("Source array elements used to fill target array null slots.");
		}

		/// <summary>
		///     Prepends elements from the source array to the beginning of the target array.
		///     Optionally, null elements in the source array can be excluded during the prepend operation.
		///     This method does not alter the existing elements in the target array.
		/// </summary>
		/// <param name="source">The source array providing elements to prepend to the target array.</param>
		/// <param name="target">The target array where elements from the source array will be prepended.</param>
		/// <param name="preserveElements">
		///     A boolean flag indicating whether to include null elements from the source array in the prepend operation.
		/// </param>
		/// <typeparam name="T">The type of elements in the arrays.</typeparam>
		public static void PrependOnly<T>(
			T[] source,
			ref T[] target,
			ElementInclusionStrategy preserveElements = ElementInclusionStrategy.Exclude
		) where T : class
		{
			// Validate source and target arrays. Log warnings and exit early if validation fails.
			if (!ValidateSource(source))
			{
				return;
			}

			// Calculate the size of the new array.
			int nonNullCount = preserveElements == ElementInclusionStrategy.Include
				? source.Length
				: source.Count(item => item != null);

			var newArray = new T[nonNullCount + target.Length];

			// Fill the first part of the array with source elements.
			var currentIndex = 0;

			foreach (var element in source)
			{
				if (preserveElements == ElementInclusionStrategy.Exclude && element == null)
				{
					continue;
				}

				newArray[currentIndex++] = element;
			}

			// Copy the existing target elements into the second part of the array.
			Array.Copy(target, 0, newArray, currentIndex, target.Length);

			// Assign the new array to the target reference.
			target = newArray;

			Logger.LogSuccess("Source array elements were prepended successfully.");
		}

		private static bool CheckForRemainingElements<T>(
			T[] source,
			int sourceIndex,
			bool excludeNulls,
			out int appendCount
		) where T : class
		{
			appendCount = 0;

			for (int i = sourceIndex ; i < source.Length ; i++)
			{
				if (!excludeNulls || source[i] != null)
				{
					appendCount++;
				}
			}

			if (appendCount != 0)
			{
				return false;
			}

			Logger.LogSuccess("Filled null elements in the target array.");
			return true;
		}

		private static bool HandleNullTargetInitialization<T>(
			T[] source,
			ref T[] target,
			bool excludeNulls
		)
			where T : class
		{
			if (target != null && target.Length != 0)
			{
				return false;
			}

			if (excludeNulls)
			{
				// Initialize target with enough space for source elements
				target = new T[ArrayHelper.CountNonNullElements(source)];

				// Fill the target with elements from the source array
				PopulateTargetWithSource(source, target, 0, true);
			}
			else
			{
				// Initialize target with source elements
				target = source.ToArray();
			}

			Logger.LogSuccess("Target was null. Initialized from source array.");
			return true;
		}

		private static bool ValidateSource<T>(T[] source) where T : class
		{
			if (source != null)
			{
				return true;
			}

			return false;
		}

		private static bool ValidateTarget<T>(T[] target) where T : class
		{
			if (target != null)
			{
				return true;
			}

			Logger.LogWarning("Target array must not be null. Aborting operation.");
			return false;
		}

		private static int PopulateTargetWithSource<T>(
			T[] source,
			T[] target,
			int sourceIndex,
			bool excludeNulls
		) where T : class
		{
			// Cache lengths for performance
			int sourceLength = source.Length;
			int targetLength = target.Length;

			for (var targetIndex = 0 ;
				 targetIndex < targetLength && sourceIndex < sourceLength ;
				 targetIndex++)
			{
				// Skip non-null entries in the target array
				if (target[targetIndex] != null)
				{
					continue;
				}

				// Find the next valid source element (non-null if exclude_nulls is true)
				T sourceElement = null;

				while (sourceIndex < sourceLength)
				{
					sourceElement = source[sourceIndex++];

					if (!excludeNulls || sourceElement != null)
					{
						break;
					}
				}

				// Assign to target only if a valid source element was found
				if (sourceElement != null || !excludeNulls)
				{
					target[targetIndex] = sourceElement;
				}
			}

			return sourceIndex;
		}

		private static void AppendValidElements<T>(
			T[] sourceArray,
			ref T[] targetArray,
			int elementsToAppend,
			int sourceStartIndex,
			bool excludeNulls = true
		) where T : class
		{
			// Combined guard clauses for early exit
			if (sourceArray.IsNullOrEmpty() || elementsToAppend == 0 || sourceStartIndex >= sourceArray.Length)
			{
				return;
			}

			Logger.LogInfo("Filling null elements and appending remaining elements from source.", nameof(ArrayHelper),
				LogCategory.Utilities);

			int originalLength = targetArray.Length;
			int newLength      = originalLength + elementsToAppend; // Introduced variable
			Array.Resize(ref targetArray, newLength);

			for (int srcIdx = sourceStartIndex, tgtIdx = originalLength ; srcIdx < sourceArray.Length ; srcIdx++)
			{
				bool shouldCopy = !excludeNulls || sourceArray[srcIdx] != null;

				if (shouldCopy)
				{
					targetArray[tgtIdx++] = sourceArray[srcIdx];
				}
			}

			Logger.LogSuccess($"Filled null elements and appended {elementsToAppend} elements from source.");
		}
	}
}
