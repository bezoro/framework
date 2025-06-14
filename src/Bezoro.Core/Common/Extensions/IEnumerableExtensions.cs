using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Bezoro.Core.Common.Extensions
{
	public static class IEnumerableExtensions
	{
		/// <summary>
		///     Returns <c>true</c> when the sequence is <c>null</c> or has no elements.
		///     Enumerates at most one element when the source is not a collection.
		///     Prefer calling the generic overload
		///     to ensure the most efficient implementation is selected.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsNullOrEmpty(this IEnumerable? enumerable)
		{
			if (enumerable is null)
				return true;

			if (enumerable is ICollection nonGeneric)
				return nonGeneric.Count == 0;

			var enumerator = enumerable.GetEnumerator();
			try
			{
				return !enumerator.MoveNext();
			}
			finally
			{
				if (enumerator is IDisposable d)
					d.Dispose();
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsNullOrEmpty<T>(this IEnumerable<T>? enumerable)
		{
			if (enumerable is null)
				return true;

			// Fast-path for mutable collections
			if (enumerable is ICollection<T> coll)
				return coll.Count == 0;

			// Fast-path for read-only collections
			if (enumerable is IReadOnlyCollection<T> readOnly)
				return readOnly.Count == 0;

			using var e = enumerable.GetEnumerator();
			return !e.MoveNext();
		}
	}
}
