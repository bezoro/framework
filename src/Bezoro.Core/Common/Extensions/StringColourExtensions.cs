// ReSharper disable InconsistentNaming

using System;
using System.Globalization;
using Bezoro.Core.Common.Primitives;

namespace Bezoro.Core.Common.Extensions;

public static class StringColourExtensions
{
	#region Core Methods

	public static string Color(this string text, string colorName) =>
		$"<color={colorName}>{text}</color>";

	public static string Color(this string text, Color color) =>
		// Always emit RGBA to preserve alpha; "#RRGGBBAA" is widely supported by rich-text engines.
		$"<color={color.ToString("RGBA", CultureInfo.InvariantCulture)}>{text}</color>";

	// Convenience: parse hex/CSS forms like "#RGB", "#RRGGBBAA", "rgba(r,g,b,a)"
	public static string ColorHex(this string text, string hexOrCss) =>
		Primitives.Color.TryParse(hexOrCss.AsSpan(), CultureInfo.InvariantCulture, out var c)
			? text.Color(c)
			: text.Color(hexOrCss); // fallback: pass through as a named color

	// Convenience: byte and float overloads
	public static string Color(this string text, byte r, byte g, byte b, byte a = 255) =>
		text.Color(Primitives.Color.FromArgb(a, r, g, b));

	public static string Color(this string text, float r, float g, float b, float a = 1f) =>
		text.Color(new Color(r, g, b, a));

	// Convenience: packed 0xRRGGBBAA
	public static string Color(this string text, uint rgba32) =>
		text.Color(Primitives.Color.FromRgba32(rgba32));

	#endregion

	#region Basics

	public static string Red(this string text) =>
		text.Color(new Color(1f, 0f, 0f, 1f));

	public static string Green(this string text) =>
		text.Color(new Color(0f, 1f, 0f, 1f));

	public static string Blue(this string text) =>
		text.Color(new Color(0f, 0f, 1f, 1f));

	public static string Yellow(this string text) =>
		text.Color(new Color(1f, 1f, 0f, 1f));

	public static string Cyan(this string text) =>
		text.Color(new Color(0f, 1f, 1f, 1f));

	public static string Magenta(this string text) =>
		text.Color(new Color(1f, 0f, 1f, 1f));

	public static string White(this string text) =>
		text.Color(new Color(1f, 1f, 1f, 1f));

	public static string Black(this string text) =>
		text.Color(new Color(0f, 0f, 0f, 1f));

	public static string Gray(this string text) =>
		text.Color(new Color(.5f, .5f, .5f, 1f));

	public static string LightGray(this string text) =>
		text.Color(new Color(.75f, .75f, .75f, 1f));

	public static string Orange(this string text) =>
		text.Color(new Color(1f, .65f, 0f, 1f));

	public static string Purple(this string text) =>
		text.Color(new Color(.63f, .13f, .94f, 1f));

	public static string Brown(this string text) =>
		text.Color(new Color(.65f, .32f, .17f, 1f));

	#endregion

	#region Reds

	public static string Pink(this string text) =>
		text.Color(new Color(1f, 0.75f, 0.8f, 1f));

	public static string LightRed(this string text) =>
		text.Color(new Color(1f, 0.4f, 0.4f, 1f));

	public static string Crimson(this string text) =>
		text.Color(new Color(0.86f, 0.08f, 0.24f, 1f));

	public static string DarkRed(this string text) =>
		text.Color(new Color(0.55f, 0f, 0f, 1f));

	public static string Maroon(this string text) =>
		text.Color(new Color(0.5f, 0f, 0f, 1f));

	public static string IndianRed(this string text) =>
		text.Color(new Color(0.8f, 0.36f, 0.36f, 1f));

	public static string FireBrick(this string text) =>
		text.Color(new Color(0.7f, 0.13f, 0.13f, 1f));

	public static string Salmon(this string text) =>
		text.Color(new Color(0.98f, 0.5f, 0.45f, 1f));

	public static string Coral(this string text) =>
		text.Color(new Color(1f, 0.5f, 0.31f, 1f));

	public static string Tomato(this string text) =>
		text.Color(new Color(1f, 0.39f, 0.28f, 1f));

	#endregion

	#region Blues

	public static string SkyBlue(this string text) =>
		text.Color(new Color(0.53f, 0.81f, 0.92f, 1f));

	public static string LightBlue(this string text) =>
		text.Color(new Color(0.68f, 0.85f, 0.90f, 1f));

	public static string DeepBlue(this string text) =>
		text.Color(new Color(0f, 0.05f, 0.61f, 1f));

	public static string Navy(this string text) =>
		text.Color(new Color(0f, 0f, 0.5f, 1f));

	public static string RoyalBlue(this string text) =>
		text.Color(new Color(0.25f, 0.41f, 0.88f, 1f));

	public static string CornflowerBlue(this string text) =>
		text.Color(new Color(0.39f, 0.58f, 0.93f, 1f));

	public static string SteelBlue(this string text) =>
		text.Color(new Color(0.27f, 0.51f, 0.71f, 1f));

	public static string DodgerBlue(this string text) =>
		text.Color(new Color(0.12f, 0.56f, 1f, 1f));

	public static string DeepSkyBlue(this string text) =>
		text.Color(new Color(0f, 0.75f, 1f, 1f));

	public static string Teal(this string text) =>
		text.Color(new Color(0f, 0.5f, 0.5f, 1f));

	#endregion

	#region Greens

	public static string LightGreen(this string text) =>
		text.Color(new Color(0.56f, 0.93f, 0.56f, 1f));

	public static string DarkGreen(this string text) =>
		text.Color(new Color(0f, 0.39f, 0f, 1f));

	public static string ForestGreen(this string text) =>
		text.Color(new Color(0.13f, 0.55f, 0.13f, 1f));

	public static string Lime(this string text) =>
		text.Color(new Color(0f, 1f, 0f, 1f));

	public static string Olive(this string text) =>
		text.Color(new Color(0.5f, 0.5f, 0f, 1f));

	public static string MediumSeaGreen(this string text) =>
		text.Color(new Color(0.24f, 0.7f, 0.44f, 1f));

	public static string SpringGreen(this string text) =>
		text.Color(new Color(0f, 1f, 0.5f, 1f));

	public static string SeaGreen(this string text) =>
		text.Color(new Color(0.18f, 0.55f, 0.34f, 1f));

	public static string PaleGreen(this string text) =>
		text.Color(new Color(0.6f, 0.98f, 0.6f, 1f));

	public static string YellowGreen(this string text) =>
		text.Color(new Color(0.6f, 0.8f, 0.2f, 1f));

	#endregion

	#region Yellows

	public static string Gold(this string text) =>
		text.Color(new Color(1f, 0.84f, 0f, 1f));

	public static string LightYellow(this string text) =>
		text.Color(new Color(1f, 1f, 0.88f, 1f));

	public static string LemonChiffon(this string text) =>
		text.Color(new Color(1f, 0.98f, 0.80f, 1f));

	public static string Khaki(this string text) =>
		text.Color(new Color(0.94f, 0.90f, 0.55f, 1f));

	public static string DarkKhaki(this string text) =>
		text.Color(new Color(0.74f, 0.72f, 0.42f, 1f));

	public static string Goldenrod(this string text) =>
		text.Color(new Color(0.85f, 0.65f, 0.13f, 1f));

	public static string DarkGoldenrod(this string text) =>
		text.Color(new Color(0.72f, 0.53f, 0.04f, 1f));

	public static string Amber(this string text) =>
		text.Color(new Color(1f, 0.75f, 0f, 1f));

	#endregion

	#region Purples

	public static string Lavender(this string text) =>
		text.Color(new Color(230, 230, 250, 255));

	public static string Violet(this string text) =>
		text.Color(new Color(238, 130, 238, 255));

	public static string Plum(this string text) =>
		text.Color(new Color(221, 160, 221, 255));

	public static string Orchid(this string text) =>
		text.Color(new Color(218, 112, 214, 255));

	public static string MediumPurple(this string text) =>
		text.Color(new Color(147, 112, 219, 255));

	public static string DarkOrchid(this string text) =>
		text.Color(new Color(153, 50, 204, 255));

	public static string DarkViolet(this string text) =>
		text.Color(new Color(148, 0, 211, 255));

	public static string BlueViolet(this string text) =>
		text.Color(new Color(138, 43, 226, 255));

	public static string Indigo(this string text) =>
		text.Color(new Color(75, 0, 130, 255));

	public static string MediumOrchid(this string text) =>
		text.Color(new Color(186, 85, 211, 255));

	#endregion

	#region Browns

	public static string SandyBrown(this string text) =>
		text.Color(new Color(244, 164, 96, 255));

	public static string RosyBrown(this string text) =>
		text.Color(new Color(188, 143, 143, 255));

	public static string Peru(this string text) =>
		text.Color(new Color(205, 133, 63, 255));

	public static string Chocolate(this string text) =>
		text.Color(new Color(210, 105, 30, 255));

	public static string SaddleBrown(this string text) =>
		text.Color(new Color(139, 69, 19, 255));

	public static string Sienna(this string text) =>
		text.Color(new Color(160, 82, 45, 255));

	public static string Tan(this string text) =>
		text.Color(new Color(210, 180, 140, 255));

	public static string BurlyWood(this string text) =>
		text.Color(new Color(222, 184, 135, 255));

	#endregion

	#region Oranges

	public static string DarkOrange(this string text) =>
		text.Color(new Color(255, 140, 0, 255));

	public static string LightOrange(this string text) =>
		text.Color(new Color(255, 179, 71, 255)); // soft orange

	public static string OrangeRed(this string text) =>
		text.Color(new Color(255, 69, 0, 255));

	public static string Peach(this string text) =>
		text.Color(new Color(255, 218, 185, 255)); // Peach Puff

	public static string Tangerine(this string text) =>
		text.Color(new Color(242, 133, 0, 255));

	#endregion

	#region Grays

	public static string DarkGray(this string text) =>
		text.Color(new Color(169, 169, 169, 255));

	public static string DimGray(this string text) =>
		text.Color(new Color(105, 105, 105, 255));

	public static string Silver(this string text) =>
		text.Color(new Color(192, 192, 192, 255));

	public static string WhiteSmoke(this string text) =>
		text.Color(new Color(245, 245, 245, 255));

	public static string Gainsboro(this string text) =>
		text.Color(new Color(220, 220, 220, 255));

	public static string SlateGray(this string text) =>
		text.Color(new Color(112, 128, 144, 255));

	#endregion

	#region Pastels

	public static string PastelPink(this string text) =>
		text.Color(new Color(255, 209, 220, 255));

	public static string PastelBlue(this string text) =>
		text.Color(new Color(174, 198, 207, 255));

	public static string PastelGreen(this string text) =>
		text.Color(new Color(119, 221, 119, 255));

	public static string PastelYellow(this string text) =>
		text.Color(new Color(253, 253, 150, 255));

	public static string PastelPurple(this string text) =>
		text.Color(new Color(195, 177, 225, 255));

	public static string PastelOrange(this string text) =>
		text.Color(new Color(255, 179, 71, 255));

	public static string PastelTurquoise(this string text) =>
		text.Color(new Color(153, 230, 230, 255));

	public static string PastelLavender(this string text) =>
		text.Color(new Color(227, 228, 250, 255));

	#endregion

	#region Neons

	public static string NeonPink(this string text) =>
		text.Color(new Color(255, 110, 199, 255));

	public static string NeonGreen(this string text) =>
		text.Color(new Color(57, 255, 20, 255));

	public static string NeonBlue(this string text) =>
		text.Color(new Color(31, 81, 255, 255));

	public static string NeonYellow(this string text) =>
		text.Color(new Color(255, 255, 51, 255));

	public static string NeonOrange(this string text) =>
		text.Color(new Color(255, 95, 31, 255));

	public static string NeonPurple(this string text) =>
		text.Color(new Color(188, 19, 254, 255));

	#endregion
}
