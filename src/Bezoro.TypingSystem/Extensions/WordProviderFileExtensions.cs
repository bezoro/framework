using System.Security;
using Bezoro.TypingSystem.Types;

namespace Bezoro.TypingSystem.Extensions;

/// <summary>
///     Provides file-loading operations for word providers.
/// </summary>
public static class WordProviderFileExtensions
{
	/// <summary>
	///     Appends each line from a file to the specified word provider.
	/// </summary>
	/// <param name="provider">The provider to receive the words.</param>
	/// <param name="filePath">The path to the file containing one word per line.</param>
	/// <exception cref="ArgumentNullException"><paramref name="provider" /> or <paramref name="filePath" /> is <see langword="null" />.</exception>
	/// <exception cref="ArgumentException"><paramref name="filePath" /> is empty, contains only white-space characters, or contains invalid characters.</exception>
	/// <exception cref="FileNotFoundException">The file specified by <paramref name="filePath" /> was not found.</exception>
	/// <exception cref="DirectoryNotFoundException">The specified path is invalid.</exception>
	/// <exception cref="IOException">An I/O error occurred while opening or reading the file.</exception>
	/// <exception cref="SecurityException">The caller does not have the required permission.</exception>
	/// <exception cref="UnauthorizedAccessException">Access to <paramref name="filePath" /> is denied.</exception>
	public static void LoadWordsFromFile(this ArrayWordProvider provider, string filePath)
	{
		if (provider is null) throw new ArgumentNullException(nameof(provider));
		if (filePath is null) throw new ArgumentNullException(nameof(filePath));

		foreach (string word in File.ReadLines(filePath)) provider.AddWord(word.AsMemory());
	}
}
