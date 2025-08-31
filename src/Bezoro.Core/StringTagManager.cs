using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Bezoro.Core;

public static class StringTags
{
	private static readonly ConcurrentDictionary<string, Func<object>> Tags = new(StringComparer.Ordinal);
	private static readonly Regex TagNamePattern = new(@"^\w+$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
	private static readonly Regex TagPattern = new(
		@"(?<!\\)\{(\w+)\}",
		RegexOptions.Compiled | RegexOptions.CultureInvariant);

	/// <summary>
	///     Get all registered tag names
	/// </summary>
	public static IEnumerable<string> GetRegisteredTags() => Tags.Keys;

	/// <summary>
	///     Process a string and replace all registered tags
	///     Escaped braces (\{tag\}) are treated as literals.
	/// </summary>
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
					// If a provider throws, leave the original tag untouched
					return match.Value;
				}
			});

		// Unescape any escaped braces
		return result.Replace("\\{", "{").Replace("\\}", "}");
	}

	/// <summary>
	///     Clear all registered tags
	/// </summary>
	public static void Clear()
	{
		Tags.Clear();
	}

	/// <summary>
	///     Register a tag with a value provider
	/// </summary>
	/// <param name="tagName">The name of the tag</param>
	/// <param name="valueProvider">Function that provides the tag value</param>
	/// <param name="allowOverwrite">Whether to allow overwriting existing tags</param>
	/// <exception cref="InvalidOperationException">Thrown when tag already exists and allowOverwrite is false</exception>
	public static void Register(string tagName, Func<object> valueProvider, bool allowOverwrite = false)
	{
		if (valueProvider is null) throw new ArgumentNullException(nameof(valueProvider));
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
	///     Register a tag with a constant value
	/// </summary>
	/// <param name="tagName">The name of the tag</param>
	/// <param name="value">Constant value to be used for the tag</param>
	/// <param name="allowOverwrite">Whether to allow overwriting existing tags</param>
	public static void RegisterValue(string tagName, object value, bool allowOverwrite = false)
	{
		Register(tagName, () => value, allowOverwrite);
	}

	/// <summary>
	///     Remove a registered tag
	/// </summary>
	public static void Unregister(string tagName)
	{
		Tags.TryRemove(tagName, out _);
	}
}
