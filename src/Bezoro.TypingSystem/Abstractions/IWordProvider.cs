using System;
using System.Collections.Generic;

namespace Bezoro.TypingSystem.Abstractions;

/// <summary>
///     Provides words for the typing system.
/// </summary>
public interface IWordProvider
{
	/// <summary>
	///     Gets a value indicating whether there are more words available.
	/// </summary>
	bool HasMoreWords { get; }

	/// <summary>
	///     Gets the total number of words available.
	/// </summary>
	uint WordCount { get; }

	/// <summary>
	///     Gets the next word from the provider.
	/// </summary>
	/// <returns>A <see cref="ReadOnlyMemory{T}"/> containing the next word.</returns>
	ReadOnlyMemory<char> GetNextWord();

	/// <summary>
	///     Adds a word to the provider.
	/// </summary>
	/// <param name="word">The word to add.</param>
	void AddWord(ReadOnlyMemory<char> word);

	/// <summary>
	///     Adds multiple words to the provider.
	/// </summary>
	/// <param name="words">The words to add.</param>
	void AddWords(IEnumerable<ReadOnlyMemory<char>> words);

	/// <summary>
	///     Adds words from a file to the provider.
	/// </summary>
	/// <param name="filePath">The path to the file containing words.</param>
	void AddWordsFromFile(string filePath);

	/// <summary>
	///     Clears all words from the provider.
	/// </summary>
	void ClearWords();

	/// <summary>
	///     Removes a specific word from the provider.
	/// </summary>
	/// <param name="word">The word to remove.</param>
	void RemoveWord(ReadOnlyMemory<char> word);
}
