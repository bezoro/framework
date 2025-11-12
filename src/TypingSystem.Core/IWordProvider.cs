using System;
using System.Collections.Generic;

namespace TypingSystem.Core;

public interface IWordProvider
{
	bool HasMoreWords { get; }

	ReadOnlyMemory<char> GetNextWord();

	void RemoveWord(ReadOnlyMemory<char> word);

	void ClearWords();

	void AddWord(ReadOnlyMemory<char> word);

	void AddWords(IEnumerable<ReadOnlyMemory<char>> words);

	void AddWordsFromFile(string filePath);

	uint WordCount { get; }
}
