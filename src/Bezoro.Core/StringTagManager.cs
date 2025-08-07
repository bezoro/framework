using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Bezoro.Core;

public static class StringTags
{
	private static readonly Dictionary<string, Func<object>> _tags       = new();
	private static readonly Regex                            _tagPattern = new(@"\{(\w+)\}", RegexOptions.Compiled);

	/// <summary>
	///     Get all registered tag names
	/// </summary>
	public static IEnumerable<string> GetRegisteredTags() =>
		_tags.Keys;

	/// <summary>
	///     Process a string and replace all registered tags
	/// </summary>
	public static string Process(string input)
	{
		if (string.IsNullOrEmpty(input)) return input;

		return _tagPattern.Replace(
			input,
			match =>
			{
				string tagName = match.Groups[1].Value;
				return _tags.TryGetValue(tagName, out var provider)
						   ? provider()?.ToString() ?? string.Empty
						   : match.Value;
			});
	}

	/// <summary>
	///     Clear all registered tags
	/// </summary>
	public static void Clear()
	{
		_tags.Clear();
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
		if (!allowOverwrite && _tags.ContainsKey(tagName))
		{
			throw new InvalidOperationException(
				$"Tag '{tagName}' is already registered. Use allowOverwrite parameter to replace it.");
		}

		_tags[tagName] = valueProvider;
	}

	/// <summary>
	///     Remove a registered tag
	/// </summary>
	public static void Unregister(string tagName)
	{
		_tags.Remove(tagName);
	}
}
