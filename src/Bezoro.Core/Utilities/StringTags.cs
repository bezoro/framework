using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Bezoro.Core.Utilities;

/// <summary>
///     Provides a thread-safe manager for registering, retrieving, and substituting string tags in text.
///     Tags are registered globally and can be replaced with dynamic or constant values in incoming strings.
/// </summary>
public static class StringTags
{
	/// <summary>
	///     Stores the mapping of tag names to their associated value provider functions.
	/// </summary>
	private static readonly ConcurrentDictionary<string, Func<object>> Tags = new(StringComparer.Ordinal);

	/// <summary>
	///     Regular expression used to validate allowed tag name formats (letters, digits, underscores).
	/// </summary>
	private static readonly Regex TagNamePattern = new(@"^\w+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

	/// <summary>
	///     Regular expression to parse unescaped tag references in the form {tagName} within input strings.
	///     Supports escaping with backslashes (e.g., \{tagName\}).
	/// </summary>
	private static readonly Regex TagPattern = new(
		@"(?<!\\)\{(\w+)\}",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);

	/// <summary>
	///     Gets an enumerable containing all registered tag names.
	/// </summary>
	/// <returns>A collection of currently registered tag names.</returns>
	public static IEnumerable<string> GetRegisteredTags() => Tags.Keys;

	/// <summary>
	///     Processes an input string, replacing each registered tag reference with its resolved value.
	///     Tag references must be in the form <c>{tagName}</c>.
	///     Escaped braces (e.g. <c>\{tag\}</c>) are left as literals.
	/// </summary>
	/// <param name="input">The input string to process.</param>
	/// <returns>
	///     The processed string, with all valid tag references replaced by their resolved values.
	///     Escaped tags are unescaped to braces.
	/// </returns>
	public static string Process(string input)
	{
		if (string.IsNullOrEmpty(input)) return input;

		string result = TagPattern.Replace(
			input,
			match =>
			{
				string tagName = match.Groups[1].Value;
				if (!Tags.TryGetValue(tagName, out var provider)) return match.Value;

				try
				{
					object? value = provider?.Invoke();
					return value?.ToString() ?? string.Empty;
				}
				catch
				{
					// If a provider throws an exception, the tag is left untouched.
					return match.Value;
				}
			});

		// Unescape any escaped braces
		return result.Replace("\\{", "{").Replace("\\}", "}");
	}

	/// <summary>
	///     Removes all registered tags from the manager.
	/// </summary>
	public static void Clear()
	{
		Tags.Clear();
	}

	/// <summary>
	///     Registers a tag with a value provider function.
	/// </summary>
	/// <param name="tagName">
	///     The name of the tag to register. Must be non-null and consist only of letters, digits, or
	///     underscores.
	/// </param>
	/// <param name="valueProvider">A delegate that provides the value for this tag.</param>
	/// <param name="allowOverwrite">
	///     If <c>true</c>, overwrites any tag already registered with the same name. If <c>false</c>,
	///     throws if the tag exists.
	/// </param>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="valueProvider" /> is <c>null</c>.</exception>
	/// <exception cref="ArgumentException">
	///     Thrown if <paramref name="tagName" /> is null, empty, whitespace, or contains
	///     invalid characters.
	/// </exception>
	/// <exception cref="InvalidOperationException">
	///     Thrown if the tag is already registered and
	///     <paramref name="allowOverwrite" /> is <c>false</c>.
	/// </exception>
	public static void Register(string tagName, Func<object> valueProvider, bool allowOverwrite = false)
	{
		if (valueProvider is null)
			throw new ArgumentNullException(nameof(valueProvider));

		if (string.IsNullOrWhiteSpace(tagName))
			throw new ArgumentException("Tag name cannot be null or whitespace.", nameof(tagName));

		tagName = tagName.Trim();
		if (!TagNamePattern.IsMatch(tagName))
			throw new ArgumentException("Tag name must contain only letters, digits, or underscores.", nameof(tagName));

		if (!allowOverwrite)
		{
			if (!Tags.TryAdd(tagName, valueProvider))
				throw new InvalidOperationException(
					$"Tag '{tagName}' is already registered. Use allowOverwrite parameter to replace it.");
		}
		else
		{
			Tags.AddOrUpdate(tagName, valueProvider, (_, _) => valueProvider);
		}
	}

	/// <summary>
	///     Registers a tag that always returns a constant value.
	/// </summary>
	/// <param name="tagName">The name of the tag to register.</param>
	/// <param name="value">A constant value to be used for this tag.</param>
	/// <param name="allowOverwrite">
	///     If <c>true</c>, overwrites any tag already registered with the same name. If <c>false</c>,
	///     throws if the tag exists.
	/// </param>
	public static void RegisterValue(string tagName, object value, bool allowOverwrite = false)
	{
		Register(tagName, () => value, allowOverwrite);
	}

	/// <summary>
	///     Removes a registered tag by name, if it exists.
	/// </summary>
	/// <param name="tagName">The name of the tag to remove.</param>
	public static void Unregister(string tagName)
	{
		Tags.TryRemove(tagName, out _);
	}
}
