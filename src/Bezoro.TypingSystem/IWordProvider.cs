using System;
using System.Collections.Generic;

namespace Bezoro.TypingSystem;

public interface IWordProvider
{
	bool HasMoreWords { get; }

	uint WordCount { get; }

	ReadOnlyMemory<char> GetNextWord();

	void AddWord(ReadOnlyMemory<char> word);

	void AddWords(IEnumerable<ReadOnlyMemory<char>> words);

	void AddWordsFromFile(string filePath);

	void ClearWords();

	void RemoveWord(ReadOnlyMemory<char> word);
}
