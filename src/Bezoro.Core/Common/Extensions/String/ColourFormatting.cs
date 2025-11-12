// ReSharper disable InconsistentNaming

using System;
using System.Globalization;
using Bezoro.Core.Common.Primitives;

namespace Bezoro.Core.Common.Extensions.String;

/// <summary>
/// Provides extension methods for formatting strings with color tags.
/// Includes convenience methods for common colors and color forms.
/// </summary>
public static class ColourFormatting
{
	#region Core Methods

	/// <summary>
	/// Wraps the string in a color tag with the given color name.
	/// </summary>
	/// <param name="text">The string to colorize.</param>
	/// <param name="colorName">The color name (e.g., "red", "green", etc.).</param>
	/// <returns>The string wrapped in a &lt;color&gt; tag.</returns>
	public static string Color(this string text, string colorName) =>
		$"<color={colorName}>{text}</color>";

	/// <summary>
	/// Wraps the string in a color tag using the given Color struct (emits #RRGGBBAA to support alpha).
	/// </summary>
	/// <param name="text">The string to colorize.</param>
	/// <param name="color">The Color object (RGBA).</param>
	/// <returns>The string wrapped in a &lt;color&gt; tag.</returns>
	public static string Color(this string text, Color color) =>
		// Always emit RGBA to preserve alpha; "#RRGGBBAA" is widely supported by rich-text engines.
		$"<color={color.ToString("RGBA", CultureInfo.InvariantCulture)}>{text}</color>";

	/// <summary>
	/// Wraps the string in a color tag using a color specified in CSS/hex format.
	/// Accepts strings like "#RGB", "#RRGGBBAA", or "rgba(r,g,b,a)".
	/// If parse fails, falls back to color name.
	/// </summary>
	/// <param name="text">The string to colorize.</param>
	/// <param name="hexOrCss">Hex/CSS color string or color name.</param>
	/// <returns>The string wrapped in a &lt;color&gt; tag.</returns>
	public static string ColorHex(this string text, string hexOrCss) =>
		Primitives.Color.TryParse(hexOrCss.AsSpan(), CultureInfo.InvariantCulture, out var c)
			? text.Color(c)
			: text.Color(hexOrCss); // fallback: pass through as a named color

	/// <summary>
	/// Wraps the string in a color tag using RGBA components as bytes.
	/// </summary>
	/// <param name="text">The string to colorize.</param>
	/// <param name="r">Red (0-255).</param>
	/// <param name="g">Green (0-255).</param>
	/// <param name="b">Blue (0-255).</param>
	/// <param name="a">Alpha (0-255, optional, default is 255).</param>
	/// <returns>The string wrapped in a &lt;color&gt; tag.</returns>
	public static string Color(this string text, byte r, byte g, byte b, byte a = 255) =>
		text.Color(Primitives.Color.FromArgb(a, r, g, b));

	/// <summary>
	/// Wraps the string in a color tag using RGBA components as floats (0.0–1.0).
	/// </summary>
	/// <param name="text">The string to colorize.</param>
	/// <param name="r">Red (0.0–1.0).</param>
	/// <param name="g">Green (0.0–1.0).</param>
	/// <param name="b">Blue (0.0–1.0).</param>
	/// <param name="a">Alpha (0.0–1.0, optional, default is 1.0).</param>
	/// <returns>The string wrapped in a &lt;color&gt; tag.</returns>
	public static string Color(this string text, float r, float g, float b, float a = 1f) =>
		text.Color(new Color(r, g, b, a));

	/// <summary>
	/// Wraps the string in a color tag using a packed RGBA uint (0xRRGGBBAA).
	/// </summary>
	/// <param name="text">The string to colorize.</param>
	/// <param name="rgba32">A packed RGBA color value.</param>
	/// <returns>The string wrapped in a &lt;color&gt; tag.</returns>
	public static string Color(this string text, uint rgba32) =>
		text.Color(Primitives.Color.FromRgba32(rgba32));

	#endregion

	#region Basics

	/// <summary>Colors the string Red (1,0,0).</summary>
	public static string Red(this string text) =>
		text.Color(new Color(1f, 0f, 0f, 1f));

	/// <summary>Colors the string Green (0,1,0).</summary>
	public static string Green(this string text) =>
		text.Color(new Color(0f, 1f, 0f, 1f));

	/// <summary>Colors the string Blue (0,0,1).</summary>
	public static string Blue(this string text) =>
		text.Color(new Color(0f, 0f, 1f, 1f));

	/// <summary>Colors the string Yellow (1,1,0).</summary>
	public static string Yellow(this string text) =>
		text.Color(new Color(1f, 1f, 0f, 1f));

	/// <summary>Colors the string Cyan (0,1,1).</summary>
	public static string Cyan(this string text) =>
		text.Color(new Color(0f, 1f, 1f, 1f));

	/// <summary>Colors the string Magenta (1,0,1).</summary>
	public static string Magenta(this string text) =>
		text.Color(new Color(1f, 0f, 1f, 1f));

	/// <summary>Colors the string White (1,1,1).</summary>
	public static string White(this string text) =>
		text.Color(new Color(1f, 1f, 1f, 1f));

	/// <summary>Colors the string Black (0,0,0).</summary>
	public static string Black(this string text) =>
		text.Color(new Color(0f, 0f, 0f, 1f));

	/// <summary>Colors the string Gray (0.5,0.5,0.5).</summary>
	public static string Gray(this string text) =>
		text.Color(new Color(.5f, .5f, .5f, 1f));

	/// <summary>Colors the string LightGray (0.75,0.75,0.75).</summary>
	public static string LightGray(this string text) =>
		text.Color(new Color(.75f, .75f, .75f, 1f));

	/// <summary>Colors the string Orange (1,0.65,0).</summary>
	public static string Orange(this string text) =>
		text.Color(new Color(1f, .65f, 0f, 1f));

	/// <summary>Colors the string Purple (0.63,0.13,0.94).</summary>
	public static string Purple(this string text) =>
		text.Color(new Color(.63f, .13f, .94f, 1f));

	/// <summary>Colors the string Brown (0.65,0.32,0.17).</summary>
	public static string Brown(this string text) =>
		text.Color(new Color(.65f, .32f, .17f, 1f));

	#endregion

	#region Reds

	/// <summary>Colors the string Pink.</summary>
	public static string Pink(this string text) =>
		text.Color(new Color(1f, 0.75f, 0.8f, 1f));
	/// <summary>Colors the string LightRed.</summary>
	public static string LightRed(this string text) =>
		text.Color(new Color(1f, 0.4f, 0.4f, 1f));
	/// <summary>Colors the string Crimson.</summary>
	public static string Crimson(this string text) =>
		text.Color(new Color(0.86f, 0.08f, 0.24f, 1f));
	/// <summary>Colors the string DarkRed.</summary>
	public static string DarkRed(this string text) =>
		text.Color(new Color(0.55f, 0f, 0f, 1f));
	/// <summary>Colors the string Maroon.</summary>
	public static string Maroon(this string text) =>
		text.Color(new Color(0.5f, 0f, 0f, 1f));
	/// <summary>Colors the string IndianRed.</summary>
	public static string IndianRed(this string text) =>
		text.Color(new Color(0.8f, 0.36f, 0.36f, 1f));
	/// <summary>Colors the string FireBrick.</summary>
	public static string FireBrick(this string text) =>
		text.Color(new Color(0.7f, 0.13f, 0.13f, 1f));
	/// <summary>Colors the string Salmon.</summary>
	public static string Salmon(this string text) =>
		text.Color(new Color(0.98f, 0.5f, 0.45f, 1f));
	/// <summary>Colors the string Coral.</summary>
	public static string Coral(this string text) =>
		text.Color(new Color(1f, 0.5f, 0.31f, 1f));
	/// <summary>Colors the string Tomato.</summary>
	public static string Tomato(this string text) =>
		text.Color(new Color(1f, 0.39f, 0.28f, 1f));

	#endregion

	#region Blues

	/// <summary>Colors the string SkyBlue.</summary>
	public static string SkyBlue(this string text) =>
		text.Color(new Color(0.53f, 0.81f, 0.92f, 1f));
	/// <summary>Colors the string LightBlue.</summary>
	public static string LightBlue(this string text) =>
		text.Color(new Color(0.68f, 0.85f, 0.90f, 1f));
	/// <summary>Colors the string DeepBlue.</summary>
	public static string DeepBlue(this string text) =>
		text.Color(new Color(0f, 0.05f, 0.61f, 1f));
	/// <summary>Colors the string Navy.</summary>
	public static string Navy(this string text) =>
		text.Color(new Color(0f, 0f, 0.5f, 1f));
	/// <summary>Colors the string RoyalBlue.</summary>
	public static string RoyalBlue(this string text) =>
		text.Color(new Color(0.25f, 0.41f, 0.88f, 1f));
	/// <summary>Colors the string CornflowerBlue.</summary>
	public static string CornflowerBlue(this string text) =>
		text.Color(new Color(0.39f, 0.58f, 0.93f, 1f));
	/// <summary>Colors the string SteelBlue.</summary>
	public static string SteelBlue(this string text) =>
		text.Color(new Color(0.27f, 0.51f, 0.71f, 1f));
	/// <summary>Colors the string DodgerBlue.</summary>
	public static string DodgerBlue(this string text) =>
		text.Color(new Color(0.12f, 0.56f, 1f, 1f));
	/// <summary>Colors the string DeepSkyBlue.</summary>
	public static string DeepSkyBlue(this string text) =>
		text.Color(new Color(0f, 0.75f, 1f, 1f));
	/// <summary>Colors the string Teal.</summary>
	public static string Teal(this string text) =>
		text.Color(new Color(0f, 0.5f, 0.5f, 1f));

	#endregion

	#region Greens

	/// <summary>Colors the string LightGreen.</summary>
	public static string LightGreen(this string text) =>
		text.Color(new Color(0.56f, 0.93f, 0.56f, 1f));
	/// <summary>Colors the string DarkGreen.</summary>
	public static string DarkGreen(this string text) =>
		text.Color(new Color(0f, 0.39f, 0f, 1f));
	/// <summary>Colors the string ForestGreen.</summary>
	public static string ForestGreen(this string text) =>
		text.Color(new Color(0.13f, 0.55f, 0.13f, 1f));
	/// <summary>Colors the string Lime.</summary>
	public static string Lime(this string text) =>
		text.Color(new Color(0f, 1f, 0f, 1f));
	/// <summary>Colors the string Olive.</summary>
	public static string Olive(this string text) =>
		text.Color(new Color(0.5f, 0.5f, 0f, 1f));
	/// <summary>Colors the string MediumSeaGreen.</summary>
	public static string MediumSeaGreen(this string text) =>
		text.Color(new Color(0.24f, 0.7f, 0.44f, 1f));
	/// <summary>Colors the string SpringGreen.</summary>
	public static string SpringGreen(this string text) =>
		text.Color(new Color(0f, 1f, 0.5f, 1f));
	/// <summary>Colors the string SeaGreen.</summary>
	public static string SeaGreen(this string text) =>
		text.Color(new Color(0.18f, 0.55f, 0.34f, 1f));
	/// <summary>Colors the string PaleGreen.</summary>
	public static string PaleGreen(this string text) =>
		text.Color(new Color(0.6f, 0.98f, 0.6f, 1f));
	/// <summary>Colors the string YellowGreen.</summary>
	public static string YellowGreen(this string text) =>
		text.Color(new Color(0.6f, 0.8f, 0.2f, 1f));

	#endregion

	#region Yellows

	/// <summary>Colors the string Gold.</summary>
	public static string Gold(this string text) =>
		text.Color(new Color(1f, 0.84f, 0f, 1f));
	/// <summary>Colors the string LightYellow.</summary>
	public static string LightYellow(this string text) =>
		text.Color(new Color(1f, 1f, 0.88f, 1f));
	/// <summary>Colors the string LemonChiffon.</summary>
	public static string LemonChiffon(this string text) =>
		text.Color(new Color(1f, 0.98f, 0.80f, 1f));
	/// <summary>Colors the string Khaki.</summary>
	public static string Khaki(this string text) =>
		text.Color(new Color(0.94f, 0.90f, 0.55f, 1f));
	/// <summary>Colors the string DarkKhaki.</summary>
	public static string DarkKhaki(this string text) =>
		text.Color(new Color(0.74f, 0.72f, 0.42f, 1f));
	/// <summary>Colors the string Goldenrod.</summary>
	public static string Goldenrod(this string text) =>
		text.Color(new Color(0.85f, 0.65f, 0.13f, 1f));
	/// <summary>Colors the string DarkGoldenrod.</summary>
	public static string DarkGoldenrod(this string text) =>
		text.Color(new Color(0.72f, 0.53f, 0.04f, 1f));
	/// <summary>Colors the string Amber.</summary>
	public static string Amber(this string text) =>
		text.Color(new Color(1f, 0.75f, 0f, 1f));

	#endregion

	#region Purples

	/// <summary>Colors the string Lavender.</summary>
	public static string Lavender(this string text) =>
		text.Color(new Color(230, 230, 250, 255));
	/// <summary>Colors the string Violet.</summary>
	public static string Violet(this string text) =>
		text.Color(new Color(238, 130, 238, 255));
	/// <summary>Colors the string Plum.</summary>
	public static string Plum(this string text) =>
		text.Color(new Color(221, 160, 221, 255));
	/// <summary>Colors the string Orchid.</summary>
	public static string Orchid(this string text) =>
		text.Color(new Color(218, 112, 214, 255));
	/// <summary>Colors the string MediumPurple.</summary>
	public static string MediumPurple(this string text) =>
		text.Color(new Color(147, 112, 219, 255));
	/// <summary>Colors the string DarkOrchid.</summary>
	public static string DarkOrchid(this string text) =>
		text.Color(new Color(153, 50, 204, 255));
	/// <summary>Colors the string DarkViolet.</summary>
	public static string DarkViolet(this string text) =>
		text.Color(new Color(148, 0, 211, 255));
	/// <summary>Colors the string BlueViolet.</summary>
	public static string BlueViolet(this string text) =>
		text.Color(new Color(138, 43, 226, 255));
	/// <summary>Colors the string Indigo.</summary>
	public static string Indigo(this string text) =>
		text.Color(new Color(75, 0, 130, 255));
	/// <summary>Colors the string MediumOrchid.</summary>
	public static string MediumOrchid(this string text) =>
		text.Color(new Color(186, 85, 211, 255));

	#endregion

	#region Browns

	/// <summary>Colors the string SandyBrown.</summary>
	public static string SandyBrown(this string text) =>
		text.Color(new Color(244, 164, 96, 255));
	/// <summary>Colors the string RosyBrown.</summary>
	public static string RosyBrown(this string text) =>
		text.Color(new Color(188, 143, 143, 255));
	/// <summary>Colors the string Peru.</summary>
	public static string Peru(this string text) =>
		text.Color(new Color(205, 133, 63, 255));
	/// <summary>Colors the string Chocolate.</summary>
	public static string Chocolate(this string text) =>
		text.Color(new Color(210, 105, 30, 255));
	/// <summary>Colors the string SaddleBrown.</summary>
	public static string SaddleBrown(this string text) =>
		text.Color(new Color(139, 69, 19, 255));
	/// <summary>Colors the string Sienna.</summary>
	public static string Sienna(this string text) =>
		text.Color(new Color(160, 82, 45, 255));
	/// <summary>Colors the string Tan.</summary>
	public static string Tan(this string text) =>
		text.Color(new Color(210, 180, 140, 255));
	/// <summary>Colors the string BurlyWood.</summary>
	public static string BurlyWood(this string text) =>
		text.Color(new Color(222, 184, 135, 255));

	#endregion

	#region Oranges

	/// <summary>Colors the string DarkOrange.</summary>
	public static string DarkOrange(this string text) =>
		text.Color(new Color(255, 140, 0, 255));
	/// <summary>Colors the string LightOrange (soft orange).</summary>
	public static string LightOrange(this string text) =>
		text.Color(new Color(255, 179, 71, 255)); // soft orange
	/// <summary>Colors the string OrangeRed.</summary>
	public static string OrangeRed(this string text) =>
		text.Color(new Color(255, 69, 0, 255));
	/// <summary>Colors the string Peach (Peach Puff).</summary>
	public static string Peach(this string text) =>
		text.Color(new Color(255, 218, 185, 255)); // Peach Puff
	/// <summary>Colors the string Tangerine.</summary>
	public static string Tangerine(this string text) =>
		text.Color(new Color(242, 133, 0, 255));

	#endregion

	#region Grays

	/// <summary>Colors the string DarkGray.</summary>
	public static string DarkGray(this string text) =>
		text.Color(new Color(169, 169, 169, 255));
	/// <summary>Colors the string DimGray.</summary>
	public static string DimGray(this string text) =>
		text.Color(new Color(105, 105, 105, 255));
	/// <summary>Colors the string Silver.</summary>
	public static string Silver(this string text) =>
		text.Color(new Color(192, 192, 192, 255));
	/// <summary>Colors the string WhiteSmoke.</summary>
	public static string WhiteSmoke(this string text) =>
		text.Color(new Color(245, 245, 245, 255));
	/// <summary>Colors the string Gainsboro.</summary>
	public static string Gainsboro(this string text) =>
		text.Color(new Color(220, 220, 220, 255));
	/// <summary>Colors the string SlateGray.</summary>
	public static string SlateGray(this string text) =>
		text.Color(new Color(112, 128, 144, 255));

	#endregion

	#region Pastels

	/// <summary>Colors the string PastelPink.</summary>
	public static string PastelPink(this string text) =>
		text.Color(new Color(255, 209, 220, 255));
	/// <summary>Colors the string PastelBlue.</summary>
	public static string PastelBlue(this string text) =>
		text.Color(new Color(174, 198, 207, 255));
	/// <summary>Colors the string PastelGreen.</summary>
	public static string PastelGreen(this string text) =>
		text.Color(new Color(119, 221, 119, 255));
	/// <summary>Colors the string PastelYellow.</summary>
	public static string PastelYellow(this string text) =>
		text.Color(new Color(253, 253, 150, 255));
	/// <summary>Colors the string PastelPurple.</summary>
	public static string PastelPurple(this string text) =>
		text.Color(new Color(195, 177, 225, 255));
	/// <summary>Colors the string PastelOrange.</summary>
	public static string PastelOrange(this string text) =>
		text.Color(new Color(255, 179, 71, 255));
	/// <summary>Colors the string PastelTurquoise.</summary>
	public static string PastelTurquoise(this string text) =>
		text.Color(new Color(153, 230, 230, 255));
	/// <summary>Colors the string PastelLavender.</summary>
	public static string PastelLavender(this string text) =>
		text.Color(new Color(227, 228, 250, 255));

	#endregion

	#region Neons

	/// <summary>Colors the string NeonPink.</summary>
	public static string NeonPink(this string text) =>
		text.Color(new Color(255, 110, 199, 255));
	/// <summary>Colors the string NeonGreen.</summary>
	public static string NeonGreen(this string text) =>
		text.Color(new Color(57, 255, 20, 255));
	/// <summary>Colors the string NeonBlue.</summary>
	public static string NeonBlue(this string text) =>
		text.Color(new Color(31, 81, 255, 255));
	/// <summary>Colors the string NeonYellow.</summary>
	public static string NeonYellow(this string text) =>
		text.Color(new Color(255, 255, 51, 255));
	/// <summary>Colors the string NeonOrange.</summary>
	public static string NeonOrange(this string text) =>
		text.Color(new Color(255, 95, 31, 255));
	/// <summary>Colors the string NeonPurple.</summary>
	public static string NeonPurple(this string text) =>
		text.Color(new Color(188, 19, 254, 255));

	#endregion
}
