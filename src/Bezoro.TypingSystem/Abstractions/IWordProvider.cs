namespace Bezoro.TypingSystem.Abstractions;

/// <summary>
///     Provides words for the typing system.
/// </summary>
public interface IWordProvider : IWordSource
{
	/// <summary>
	///     Gets a value indicating whether there are more words available.
	/// </summary>
	[Obsolete("Use TryGetNextWord(out ReadOnlyMemory<char>) instead.")]
	bool HasMoreWords { get; }

	/// <summary>
	///     Gets the total number of words available.
	/// </summary>
	[Obsolete("Use ArrayWordProvider.WordCount instead.")]
	uint WordCount { get; }

	/// <summary>
	///     Gets the next word from the provider.
	/// </summary>
	/// <returns>A <see cref="ReadOnlyMemory{T}" /> containing the next word.</returns>
	[Obsolete("Use TryGetNextWord(out ReadOnlyMemory<char>) instead.")]
	ReadOnlyMemory<char> GetNextWord();

	/// <summary>
	///     Adds a word to the provider.
	/// </summary>
	/// <param name="word">The word to add.</param>
	[Obsolete("Use ArrayWordProvider.AddWord instead.")]
	void AddWord(ReadOnlyMemory<char> word);

	/// <summary>
	///     Adds multiple words to the provider.
	/// </summary>
	/// <param name="words">The words to add.</param>
	[Obsolete("Use ArrayWordProvider.AddWords instead.")]
	void AddWords(IEnumerable<ReadOnlyMemory<char>> words);

	/// <summary>
	///     Adds words from a file to the provider.
	/// </summary>
	/// <param name="filePath">The path to the file containing words.</param>
	[Obsolete("Use WordProviderFileExtensions.LoadWordsFromFile instead.")]
	void AddWordsFromFile(string filePath);

	/// <summary>
	///     Clears all words from the provider.
	/// </summary>
	[Obsolete("Use ArrayWordProvider.ClearWords instead.")]
	void ClearWords();

	/// <summary>
	///     Removes a specific word from the provider.
	/// </summary>
	/// <param name="word">The word to remove.</param>
	[Obsolete("Use ArrayWordProvider.RemoveWord instead.")]
	void RemoveWord(ReadOnlyMemory<char> word);

	bool IWordSource.TryGetNextWord(out ReadOnlyMemory<char> word)
	{
#pragma warning disable CS0618
		if (!HasMoreWords)
		{
			word = ReadOnlyMemory<char>.Empty;
			return false;
		}

		word = GetNextWord();
#pragma warning restore CS0618
		return true;
	}
}
