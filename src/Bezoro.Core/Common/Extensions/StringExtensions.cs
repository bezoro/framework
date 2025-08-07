using System;
using System.Text;
using Bezoro.Core.Common.Primitives;

namespace Bezoro.Core.Common.Extensions;

public static class StringExtensions
{
	public static bool IsEmpty(this string text) =>
		text.AsSpan().Trim() == string.Empty;

	public static bool IsNullOrEmpty(this string? text) =>
		text == null || text.IsEmpty();

	public static string Bold(this string text) =>
		string.IsNullOrEmpty(text) ? "" : $"<b>{text}</b>";

	public static string Bracketed(this string text, int padding = 0, Color color = default, char bracket = '[')
	{
		if (string.IsNullOrWhiteSpace(text)) return string.Empty;

		char   closingBracket = GetClosingBracket(bracket);
		string paddedText     = text.PadLeft(text.Length + padding).PadRight(text.Length + 2 * padding);

		if (color == default) return $"{bracket}{paddedText}{closingBracket}";

		var colorTag      = $"<color=#{color.R:X2}{color.G:X2}{color.B:X2}>";
		var closeColorTag = "</color>";
		return $"{colorTag}{bracket}{closeColorTag}{paddedText}{colorTag}{closingBracket}{closeColorTag}";
	}

	public static string Capitalize(this string text) =>
		string.IsNullOrEmpty(text) ? "" : char.ToUpper(text[0]) + text[1..];

	public static string Italic(this string text) =>
		string.IsNullOrEmpty(text) ? "" : $"<i>{text}</i>";

	public static string Lowercase(this string text) =>
		text == null ? "" : text.ToLower();

	/// <summary>
	///     Repeats a string a specified number of times.
	/// </summary>
	/// <param name="str">The string to repeat</param>
	/// <param name="count">The number of times to repeat the string</param>
	/// <returns>A new string containing the original string repeated the specified number of times</returns>
	public static string Repeat(this string str, int count)
	{
		if (str == null) throw new ArgumentNullException(nameof(str));

		if (count < 0) throw new ArgumentOutOfRangeException(nameof(count), "Count cannot be negative");

		if (count == 0 || string.IsNullOrEmpty(str)) return string.Empty;

		if (count == 1) return str;

		var sb = new StringBuilder(str.Length * count);
		for (var i = 0; i < count; i++) sb.Append(str);

		return sb.ToString();
	}

	public static string Size(this string text, int size) =>
		string.IsNullOrEmpty(text) ? "" : $"<size={size}>{text}</size>";

	public static string Strikethrough(this string text) =>
		string.IsNullOrEmpty(text) ? "" : $"<s>{text}</s>";

	public static string Underline(this string text) =>
		string.IsNullOrEmpty(text) ? "" : $"<u>{text}</u>";

	public static string Uppercase(this string text) =>
		text == null ? "" : text.ToUpper();

	private static char GetClosingBracket(char openingBracket)
	{
		return openingBracket switch
		{
			'[' => ']',
			'(' => ')',
			'{' => '}',
			'<' => '>',
			_   => openingBracket // For symmetric brackets or custom characters
		};
	}
}
