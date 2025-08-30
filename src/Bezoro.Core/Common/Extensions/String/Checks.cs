namespace Bezoro.Core.Common.Extensions.String;

public static class Checks
{
	public static bool IsEmpty(this string text) =>
		text.Trim() == string.Empty;

	public static bool IsNullOrEmpty(this string? text) =>
		text == null || text.IsEmpty();

	public static bool IsNullOrWhiteSpace(this string? text) =>
		text == null || text.Trim() == string.Empty;

	public static bool IsWhiteSpace(this string text) =>
		text != "" && text.Trim() == string.Empty;
}
