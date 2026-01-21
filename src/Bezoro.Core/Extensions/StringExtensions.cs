namespace Bezoro.Core.Extensions;

/// <summary>
///     Provides extension methods for common string empty/null/whitespace checks, following .NET semantics.
/// </summary>
public static partial class StringExtensions
{
	/// <summary>
	///     Determines whether the string, after trimming, is empty.
	/// </summary>
	/// <param name="text">The string to check.</param>
	/// <returns>True if the trimmed string has no characters; otherwise, false.</returns>
	public static bool IsEmpty(this string text) =>
		text.Trim() == string.Empty;

	/// <summary>
	///     Determines whether the string is <c>null</c> or empty after trimming.
	/// </summary>
	/// <param name="text">The string to check.</param>
	/// <returns>True if the input is null, or empty after trim; otherwise, false.</returns>
	public static bool IsNullOrEmpty(this string? text) =>
		text == null || text.IsEmpty();

	/// <summary>
	///     Determines whether the string is <c>null</c> or consists only of whitespace.
	/// </summary>
	/// <param name="text">The string to check.</param>
	/// <returns>True if the input is null, or contains only whitespace; otherwise, false.</returns>
	public static bool IsNullOrWhiteSpace(this string? text) =>
		text == null || text.Trim() == string.Empty;

	/// <summary>
	///     Determines whether the string is not empty but consists only of whitespace.
	/// </summary>
	/// <param name="text">The string to check.</param>
	/// <returns>True if the string is not empty and only contains whitespace; otherwise, false.</returns>
	public static bool IsWhiteSpace(this string text) =>
		text != "" && text.Trim() == string.Empty;
}
