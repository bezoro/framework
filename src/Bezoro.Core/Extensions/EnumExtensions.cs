using Color = Bezoro.Core.Types.Color;

namespace Bezoro.Core.Extensions;

/// <summary>
///     Provides extension methods for <see cref="Enum" /> types,
///     including type validation and string transformation/formatting utilities.
/// </summary>
public static class EnumExtensions
{
	/// <summary>
	///     Determines whether the specified enum value is defined for its enum type.
	/// </summary>
	/// <typeparam name="T">The enum type.</typeparam>
	/// <param name="value">The enum value to check.</param>
	/// <returns><c>true</c> if the value is defined in <typeparamref name="T" />; otherwise, <c>false</c>.</returns>
	public static bool IsDefined<T>(this T value)
		where T : Enum =>
		Enum.IsDefined(typeof(T), value);

	/// <summary>
	///     Returns the string name of the enum value formatted in black.
	/// </summary>
	/// <param name="value">The enum value to format.</param>
	/// <returns>Formatted string representation of the enum value.</returns>
	[Obsolete("Use value.ToString() with the corresponding string extension instead.")]
	public static string Black(this Enum value) =>
		value.ToString().Color(new Color(0f, 0f, 0f, 1f));

	/// <summary>
	///     Returns the string name of the enum value formatted in blue.
	/// </summary>
	/// <param name="value">The enum value to format.</param>
	/// <returns>Formatted string representation of the enum value.</returns>
	[Obsolete("Use value.ToString() with the corresponding string extension instead.")]
	public static string Blue(this Enum value) =>
		value.ToString().Color(new Color(0f, 0f, 1f, 1f));

	/// <summary>
	///     Returns the string name of the enum value in bold.
	/// </summary>
	/// <param name="value">The enum value to format.</param>
	/// <returns>Formatted string representation of the enum value.</returns>
	[Obsolete("Use value.ToString() with the corresponding string extension instead.")]
	public static string Bold(this Enum value) =>
		value.ToString().Bold();

	/// <summary>
	///     Returns the string name of the enum value formatted in brown.
	/// </summary>
	/// <param name="value">The enum value to format.</param>
	/// <returns>Formatted string representation of the enum value.</returns>
	[Obsolete("Use value.ToString() with the corresponding string extension instead.")]
	public static string Brown(this Enum value) =>
		value.ToString().Color(new Color(.65f, .32f, .17f, 1f));

	/// <summary>
	///     Returns the string name of the enum value with the first character capitalized.
	/// </summary>
	/// <param name="value">The enum value to format.</param>
	/// <returns>Capitalized string representation of the enum value.</returns>
	[Obsolete("Use value.ToString() with the corresponding string extension instead.")]
	public static string Capitalize(this Enum value) =>
		value.ToString().Capitalize();

	/// <summary>
	///     Returns the string name of the enum value with the text colored as specified by <paramref name="colorName" />.
	/// </summary>
	/// <param name="value">The enum value to format.</param>
	/// <param name="colorName">The name of the color to use.</param>
	/// <returns>Colored string representation of the enum value.</returns>
	public static string Color(this Enum value, string colorName) =>
		value.ToString().Color(colorName);

	/// <summary>
	///     Returns the string name of the enum value with the text colored as specified by a
	///     <see cref="Bezoro.Core.Types.Color" /> struct.
	/// </summary>
	/// <param name="value">The enum value to format.</param>
	/// <param name="color">The color struct to apply.</param>
	/// <returns>Colored string representation of the enum value.</returns>
	public static string Color(this Enum value, Color color) =>
		value.ToString().Color(color);

	/// <summary>
	///     Returns the string name of the enum value formatted in cyan.
	/// </summary>
	/// <param name="value">The enum value to format.</param>
	/// <returns>Formatted string representation of the enum value.</returns>
	[Obsolete("Use value.ToString() with the corresponding string extension instead.")]
	public static string Cyan(this Enum value) =>
		value.ToString().Color(new Color(0f, 1f, 1f, 1f));

	/// <summary>
	///     Returns the string name of the enum value formatted in gray.
	/// </summary>
	/// <param name="value">The enum value to format.</param>
	/// <returns>Formatted string representation of the enum value.</returns>
	[Obsolete("Use value.ToString() with the corresponding string extension instead.")]
	public static string Gray(this Enum value) =>
		value.ToString().Color(new Color(.5f, .5f, .5f, 1f));

	/// <summary>
	///     Returns the string name of the enum value formatted in green.
	/// </summary>
	/// <param name="value">The enum value to format.</param>
	/// <returns>Formatted string representation of the enum value.</returns>
	[Obsolete("Use value.ToString() with the corresponding string extension instead.")]
	public static string Green(this Enum value) =>
		value.ToString().Color(new Color(0f, 1f, 0f, 1f));

	/// <summary>
	///     Returns the string name of the enum value in italic style.
	/// </summary>
	/// <param name="value">The enum value to format.</param>
	/// <returns>Italicized string representation of the enum value.</returns>
	[Obsolete("Use value.ToString() with the corresponding string extension instead.")]
	public static string Italic(this Enum value) =>
		value.ToString().Italic();

	/// <summary>
	///     Returns the string name of the enum value in all lowercase.
	/// </summary>
	/// <param name="value">The enum value to convert.</param>
	/// <returns>Lowercase string representation of the enum value.</returns>
	[Obsolete("Use value.ToString() with the corresponding string extension instead.")]
	public static string Lowercase(this Enum value) =>
		value.ToString().Lowercase();

	/// <summary>
	///     Returns the string name of the enum value formatted in magenta.
	/// </summary>
	/// <param name="value">The enum value to format.</param>
	/// <returns>Formatted string representation of the enum value.</returns>
	[Obsolete("Use value.ToString() with the corresponding string extension instead.")]
	public static string Magenta(this Enum value) =>
		value.ToString().Color(new Color(1f, 0f, 1f, 1f));

	/// <summary>
	///     Returns the string name of the enum value formatted in orange.
	/// </summary>
	/// <param name="value">The enum value to format.</param>
	/// <returns>Formatted string representation of the enum value.</returns>
	[Obsolete("Use value.ToString() with the corresponding string extension instead.")]
	public static string Orange(this Enum value) =>
		value.ToString().Color(new Color(1f, .65f, 0f, 1f));

	/// <summary>
	///     Returns the string name of the enum value formatted in purple.
	/// </summary>
	/// <param name="value">The enum value to format.</param>
	/// <returns>Formatted string representation of the enum value.</returns>
	[Obsolete("Use value.ToString() with the corresponding string extension instead.")]
	public static string Purple(this Enum value) =>
		value.ToString().Color(new Color(.63f, .13f, .94f, 1f));

	/// <summary>
	///     Returns the string name of the enum value formatted in red.
	/// </summary>
	/// <param name="value">The enum value to format.</param>
	/// <returns>Formatted string representation of the enum value.</returns>
	[Obsolete("Use value.ToString() with the corresponding string extension instead.")]
	public static string Red(this Enum value) =>
		value.ToString().Color(new Color(1f, 0f, 0f, 1f));

	/// <summary>
	///     Returns the string name of the enum value resized to the specified text size.
	/// </summary>
	/// <param name="value">The enum value to format.</param>
	/// <param name="size">The text size.</param>
	/// <returns>Resized string representation of the enum value.</returns>
	[Obsolete("Use value.ToString() with the corresponding string extension instead.")]
	public static string Size(this Enum value, int size) =>
		value.ToString().Size(size);

	/// <summary>
	///     Returns the string name of the enum value with a strikethrough applied.
	/// </summary>
	/// <param name="value">The enum value to format.</param>
	/// <returns>String representation of the enum value with strikethrough.</returns>
	[Obsolete("Use value.ToString() with the corresponding string extension instead.")]
	public static string Strikethrough(this Enum value) =>
		value.ToString().Strikethrough();

	/// <summary>
	///     Returns the string name of the enum value with an underline applied.
	/// </summary>
	/// <param name="value">The enum value to format.</param>
	/// <returns>String representation of the enum value with underline.</returns>
	[Obsolete("Use value.ToString() with the corresponding string extension instead.")]
	public static string Underline(this Enum value) =>
		value.ToString().Underline();

	/// <summary>
	///     Returns the string name of the enum value in all uppercase.
	/// </summary>
	/// <param name="value">The enum value to convert.</param>
	/// <returns>Uppercase string representation of the enum value.</returns>
	[Obsolete("Use value.ToString() with the corresponding string extension instead.")]
	public static string Uppercase(this Enum value) =>
		value.ToString().Uppercase();

	/// <summary>
	///     Returns the string name of the enum value formatted in white.
	/// </summary>
	/// <param name="value">The enum value to format.</param>
	/// <returns>Formatted string representation of the enum value.</returns>
	[Obsolete("Use value.ToString() with the corresponding string extension instead.")]
	public static string White(this Enum value) =>
		value.ToString().Color(new Color(1f, 1f, 1f, 1f));

	/// <summary>
	///     Returns the string name of the enum value formatted in yellow.
	/// </summary>
	/// <param name="value">The enum value to format.</param>
	/// <returns>Formatted string representation of the enum value.</returns>
	[Obsolete("Use value.ToString() with the corresponding string extension instead.")]
	public static string Yellow(this Enum value) =>
		value.ToString().Color(new Color(1f, 1f, 0f, 1f));
}
