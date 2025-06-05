using System.Collections;
using System.Linq;
using System.Runtime.CompilerServices;

// ReSharper disable PossibleMultipleEnumeration

namespace Bezoro.Core.Collections
{
	public static class IEnumerableExtensions
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsNull(this IEnumerable? enumerable) => enumerable == null;

		/// <summary>
		///     Checks if the IEnumerable is null or contains no elements.
		///     This method is robust by handling nulls, optimizing for ICollection types,
		///     and ensuring proper enumerator handling for other IEnumerable types.
		/// </summary>
		/// <param name="enumerable">The IEnumerable to check.</param>
		/// <returns>True if the enumerable is null or empty, false otherwise.</returns>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsNullOrEmpty(this IEnumerable? enumerable)
		{
			if (enumerable.IsNull())
			{
				return true;
			}

			if (enumerable is ICollection collection)
			{
				return collection.Count == 0;
			}

			// Fallback for general IEnumerable instances.
			// Using Linq's Any() after casting to IEnumerable<object> is a clean
			// and safe way to check for elements. Any() handles enumerator
			// creation and disposal correctly.
			return !enumerable!.Cast<object>().Any();
		}
	}
}
