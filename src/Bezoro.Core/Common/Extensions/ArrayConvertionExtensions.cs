using Bezoro.Core.Common.Helpers;

namespace Bezoro.Core.Common.Extensions;

public static class ArrayConvertionExtensions
{
	public static T[] To1D<T>(this T[,] from) =>
		ArrayConverter.From2Dto1D(from);
}
