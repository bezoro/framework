using Bezoro.Core.Common.Helpers;

namespace Bezoro.Core.Common.Extensions.Collections.Conversion;

public static class ArrayConversionExtensions
{
	public static T[] To1D<T>(this T[,] from) =>
		ArrayConverter.From2Dto1D(from);
}
