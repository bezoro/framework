using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Bezoro.Core.Common.Extensions
{
	/// <summary>
	///     Helpers for quick null/empty checks on arrays.
	///     A <c>null</c> array is treated as <em>not</em> empty.
	/// </summary>
	public static class ArrayExtensions
	{
		/// <summary>
		///     Returns <c>true</c> when the array is non-null and has <c>Length == 0</c>.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsEmpty<T>([NotNullWhen(true)] this T[]? array) => array?.Length == 0;

		/// <summary>
		///     Returns <c>true</c> when the array is non-null and has at least one element.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsNotEmpty<T>([NotNullWhen(true)] this T[]? array) => array?.Length > 0;

		/// <summary>
		///     Returns <c>true</c> when the array reference is non-null.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsNotNull<T>([NotNullWhen(true)] this T[]? array) => array is not null;

		/// <summary>
		///     Negation of <see cref="IsNullOrEmpty{T}" />.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsNotNullOrEmpty<T>([NotNullWhen(true)] this T[]? array) => array is { Length: > 0 };

		/// <summary>
		///     Returns <c>true</c> when the array reference is <c>null</c>.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsNull<T>([NotNullWhen(false)] this T[]? array) => array is null;

		/// <summary>
		///     Combined convenience check—mirrors <see cref="string.IsNullOrEmpty(string)" />.
		/// </summary>
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsNullOrEmpty<T>([NotNullWhen(false)] this T[]? array) => array is null || array.Length == 0;
	}
}
