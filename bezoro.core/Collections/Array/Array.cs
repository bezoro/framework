using System;
using System.Linq;

namespace Bezoro.Core.Collections.Array
{
	public static class Array
	{
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
				if (!ValidateSource(source)) return;

				var elementsToAppendCount = 0;

				// Calculate how many valid elements will be appended from the source array.
				foreach (var element in source)
				{
					if (elementInclusionStrategy == ElementInclusionStrategy.Include || element != null)
						elementsToAppendCount++;
				}

				// If there are no valid elements to append, exit early.
				if (elementsToAppendCount == 0)
				{
					Logger.LogInfo("No elements to append from the source array.");
					return;
				}

				// Resize the target array to hold the new elements.
				var originalLength = target.Length;
				System.Array.Resize(ref target, originalLength + elementsToAppendCount);

				// Append elements directly to the target array.
				var appendIndex = originalLength;

				foreach (var element in source)
				{
					if (elementInclusionStrategy == ElementInclusionStrategy.Include || element != null)
						target[appendIndex++] = element;
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
				if (source == null || target == null) return;

				// Cache the condition to skip null elements in the source
				var excludeNulls      = elementInclusionStrategy == ElementInclusionStrategy.Exclude;
				var targetInitialized = HandleNullTargetInitialization(source, ref target, excludeNulls);

				if (targetInitialized) return;

				var sourceIndex = 0;

				// Step 1: Fill null elements in target from the source array.
				sourceIndex = PopulateTargetWithSource(source, target, sourceIndex, excludeNulls);

				// Step 2: Calculate remaining valid source elements.
				if (CheckForRemainingElements(source, sourceIndex, excludeNulls, out var appendCount))
					return;

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
				if (!ValidateSource(source)) return;

				// Step 1: Fill null elements in the target array using the source array.
				var sourceIndex = 0;

				for (var targetIndex = 0 ;
					 targetIndex < target.Length && sourceIndex < source.Length ;
					 targetIndex++)
				{
					if (target[targetIndex] == null
						&& (source[sourceIndex]         != null
							|| elementInclusionStrategy == ElementInclusionStrategy.Include))
					{
						target[targetIndex] = source[sourceIndex];
					}

					if (elementInclusionStrategy == ElementInclusionStrategy.Exclude || source[sourceIndex] != null)
						sourceIndex++;
				}

				// Step 2: Prepend remaining source elements directly to the target array.
				var remainingLength = source.Length - sourceIndex;

				if (remainingLength > 0)
				{
					// Optimization: Allocate new target array once directly for final size.
					var newTarget = new T[target.Length + remainingLength];

					// Copy remaining source elements to the beginning of the new target array.
					System.Array.Copy(source, sourceIndex, newTarget, 0, remainingLength);

					// Copy the original target elements into the new array.
					System.Array.Copy(target, 0, newTarget, remainingLength, target.Length);

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
				if (!ValidateSource(source)) return;

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
					var totalNewSize  = target.Length + (source.Length - sourceIndex);
					var resizedTarget = new T[totalNewSize];

					// Use Array.Copy for better memory and performance efficiency.
					System.Array.Copy(target, resizedTarget, target.Length);

					System.Array.Copy(
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
				if (!ValidateSource(source)) return;

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
					if (target[targetIndex] != null) continue;

					// Exit early if there are no more source elements.
					if (sourceIndex >= source.Length) break;

					// Get the next element from the source array.
					var sourceElement = source[sourceIndex];

					if (sourceElement != null)
					{
						// Fill the null slot in the target array.
						target[targetIndex] = sourceElement;
						Logger.LogInfo($"Element {sourceElement} added to target array at index {targetIndex}.");
					}

					// Move to the next element in the source array.
					sourceIndex++;
				}

				// Log warning if not all source elements could be used.
				if (sourceIndex < source.Length)
				{
					Logger.Log_Warning(
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
				if (!ValidateSource(source)) return;

				// Calculate the size of the new array.
				var nonNullCount = preserveElements == ElementInclusionStrategy.Include
					? source.Length
					: source.Count(item => item != null);

				var newArray = new T[nonNullCount + target.Length];

				// Fill the first part of the array with source elements.
				var currentIndex = 0;

				foreach (var element in source)
				{
					if (preserveElements == ElementInclusionStrategy.Exclude && element == null) continue;

					newArray[currentIndex++] = element;
				}

				// Copy the existing target elements into the second part of the array.
				System.Array.Copy(target, 0, newArray, currentIndex, target.Length);

				// Assign the new array to the target reference.
				target = newArray;

				Logger.LogSuccess("Source array elements were prepended successfully.");
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
					return;

				Logger.LogInfo("Filling null elements and appending remaining elements from source.");

				var originalLength = targetArray.Length;
				var newLength      = originalLength + elementsToAppend; // Introduced variable
				System.Array.Resize(ref targetArray, newLength);

				for (int srcIdx = sourceStartIndex, tgtIdx = originalLength ; srcIdx < sourceArray.Length ; srcIdx++)
				{
					var shouldCopy = !excludeNulls || sourceArray[srcIdx] != null;

					if (shouldCopy)
						targetArray[tgtIdx++] = sourceArray[srcIdx];
				}

				Logger.LogSuccess($"Filled null elements and appended {elementsToAppend} elements from source.");
			}

			private static bool CheckForRemainingElements<T>(
				T[] source,
				int sourceIndex,
				bool excludeNulls,
				out int appendCount
			) where T : class
			{
				appendCount = 0;

				for (var i = sourceIndex ; i < source.Length ; i++)
				{
					if (!excludeNulls || source[i] != null)
						appendCount++;
				}

				if (appendCount != 0)
					return false;

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
				if (target != null && target.Length != 0) return false;

				if (excludeNulls)
				{
					// Initialize target with enough space for source elements
					target = new T[ArrayHelpers.CountNonNullElements(source)];

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

			private static int PopulateTargetWithSource<T>(
				T[] source,
				T[] target,
				int sourceIndex,
				bool excludeNulls
			) where T : class
			{
				// Cache lengths for performance
				var sourceLength = source.Length;
				var targetLength = target.Length;

				for (var targetIndex = 0 ;
					 targetIndex < targetLength && sourceIndex < sourceLength ;
					 targetIndex++)
				{
					// Skip non-null entries in the target array
					if (target[targetIndex] != null)
						continue;

					// Find the next valid source element (non-null if exclude_nulls is true)
					T sourceElement = null;

					while (sourceIndex < sourceLength)
					{
						sourceElement = source[sourceIndex++];

						if (!excludeNulls || sourceElement != null)
							break;
					}

					// Assign to target only if a valid source element was found
					if (sourceElement != null || !excludeNulls)
						target[targetIndex] = sourceElement;
				}

				return sourceIndex;
			}

			private static bool ValidateSource<T>(T[] source) where T : class
			{
				if (source != null) return true;

				Logger.Log_Exception(new NullReferenceException("Source array must not be null. Aborting operation."));
				return false;
			}

			private static bool ValidateTarget<T>(T[] target) where T : class
			{
				if (target != null) return true;

				Logger.Log_Warning("Target array must not be null. Aborting operation.");
				return false;
			}
		}
	}

	public enum ElementInclusionStrategy
	{
		Exclude = 0,
		Include
	}

	public enum MergingStrategy
	{
		Fill_Null_Elements_And_Append = 0,
		Fill_Null_Elements_And_Resize,
		Fill_Null_Elements_And_Prepend,
		Fill_Null_Elements_Only,
		Append_Only,
		Prepend_Only
	}
}
