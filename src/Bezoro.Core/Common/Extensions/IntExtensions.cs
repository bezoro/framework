namespace Bezoro.Core.Common.Extensions;

public static class IntExtensions
{
	public static bool IsEven(this int value) => value % 2 == 0;
	public static bool IsNegative(this int value) => value < 0;
	public static bool IsOdd(this int value) => value % 2 != 0;
	public static bool IsPositive(this int value) => value > 0;
	public static bool IsZero(this int value) => value == 0;
	public static int  Abs(this int value) => value < 0 ? -value : value;
	public static int  Clamp(this int value, int min, int max) => value < min ? min : value > max ? max : value;
	public static int  ClampMax(this int value, int max) => value > max ? max : value;
	public static int  ClampMin(this int value, int min) => value < min ? min : value;
	public static int  RoundToNearest(this int value, int nearest) => (value + nearest / 2) / nearest * nearest;
	public static int  Sign(this int value) => value < 0 ? -1 : 1;

	public static void ThrowIfLessThan(this int value, int min)
	{
		if (value < min) throw new ValueTooSmallException(value, min);
	}

	public static void ThrowIfMoreThan(this int value, int max)
	{
		if (value > max) throw new ValueTooLargeException(value, max);
	}
}
