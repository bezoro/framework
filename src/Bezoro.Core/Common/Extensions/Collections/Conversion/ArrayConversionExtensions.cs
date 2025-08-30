using Bezoro.Core.Common.Helpers;

namespace Bezoro.Core.Common.Extensions.Collections.Conversion;

/// <summary>
///     Provides extension methods for converting between different array dimensions.
/// </summary>
public static class ArrayConversionExtensions
{
	/// <summary>
	///     Converts a two-dimensional array to a one-dimensional array.
	/// </summary>
	/// <typeparam name="T">The type of elements in the array.</typeparam>
	/// <param name="from">The two-dimensional array to convert.</param>
	/// <returns>A one-dimensional array containing all elements from the input array.</returns>
	public static T[] To1D<T>(this T[,] from) =>
		ArrayConverter.From2Dto1D(from);
}
