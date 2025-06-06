using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Bezoro.Core.Collections.Array
{
	/// <summary>
	///     Small set of helpers for quick null / empty checks on arrays.
	///     Works the same in Unity and in plain .NET because arrays never
	///     derive from <see cref="object" />, so the custom Unity
	///     equality operator is not involved.
	/// </summary>
	public static class ArrayExtensionsUnit
	{
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsEmpty<T>(this T[]? array) =>
			array is not null && array.Length == 0;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsNotEmpty<T>([NotNullWhen(true)] this T[]? array) =>
			array is not null && array.Length > 0;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsNotNull<T>([NotNullWhen(true)] this T[]? array) =>
			array is not null;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsNotNullOrEmpty<T>([NotNullWhen(true)] this T[]? array) =>
			array is not null && array.Length > 0;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsNull<T>([NotNullWhen(false)] this T[]? array) =>
			array is null;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsNullOrEmpty<T>(this T[] array) =>
			array is null || array.Length == 0;
	}
}
