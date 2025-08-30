using System;
using Bezoro.Core.Common.Extensions.String;
using Color = Bezoro.Core.Common.Primitives.Color;

namespace Bezoro.Core.Common.Extensions;

public static class EnumExtensions
{
	public static bool Is_Defined<T>(this T value)
		where T : Enum =>
		Enum.IsDefined(typeof(T), value);

	public static string Black(this Enum value) =>
		value.ToString().Black();

	public static string Blue(this Enum value) =>
		value.ToString().Blue();

	// Text formatting
	public static string Bold(this Enum value) =>
		value.ToString().Bold();

	public static string Brown(this Enum value) =>
		value.ToString().Brown();

	public static string Capitalize(this Enum value) =>
		value.ToString().Capitalize();

	// Color applications
	public static string Color(this Enum value, string colorName) =>
		value.ToString().Color(colorName);

	public static string Color(this Enum value, Color color) =>
		value.ToString().Color(color);

	public static string Cyan(this Enum value) =>
		value.ToString().Cyan();

	public static string Gray(this Enum value) =>
		value.ToString().Gray();

	public static string Green(this Enum value) =>
		value.ToString().Green();

	public static string Italic(this Enum value) =>
		value.ToString().Italic();

	public static string Lowercase(this Enum value) =>
		value.ToString().Lowercase();

	public static string Magenta(this Enum value) =>
		value.ToString().Magenta();

	public static string Orange(this Enum value) =>
		value.ToString().Orange();

	public static string Purple(this Enum value) =>
		value.ToString().Purple();

	// Specific colors
	public static string Red(this Enum value) =>
		value.ToString().Red();

	// Size modifications
	public static string Size(this Enum value, int size) =>
		value.ToString().Size(size);

	public static string Strikethrough(this Enum value) =>
		value.ToString().Strikethrough();

	public static string Underline(this Enum value) =>
		value.ToString().Underline();

	// Case modifications
	public static string Uppercase(this Enum value) =>
		value.ToString().Uppercase();

	public static string White(this Enum value) =>
		value.ToString().White();

	public static string Yellow(this Enum value) =>
		value.ToString().Yellow();
}
