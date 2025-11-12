using System;
using System.Collections.Generic;
using System.IO;
using Bezoro.Core.Common.Extensions;
using Bezoro.Core.Common.Primitives;

namespace TypingSystem.Core;

public sealed class ArrayWordProvider : IWordProvider
{
	private readonly SwapbackArray<string> _words;
	private          uint                  _index;

	public ArrayWordProvider(IEnumerable<string> words)
	{
		words.ThrowIfNull(nameof(words));
		words.ThrowIfEmpty(nameof(words));

		_words = new SwapbackArray<string>(words);
	}

	public bool HasMoreWords => _index < _words.Count;

	public uint WordCount => _words.Count;

	public void AddWord(ReadOnlyMemory<char> word)
	{
		_words.Add(word.ToString());
	}

	public void AddWords(IEnumerable<ReadOnlyMemory<char>> words)
	{
		foreach (var word in words) _words.Add(word.ToString());
	}

	public void AddWordsFromFile(string filePath)
	{
		foreach (var word in File.ReadAllLines(filePath)) _words.Add(word);
	}

	public void ClearWords()
	{
		_words.Clear();
		_index = 0;
	}

	public void RemoveWord(ReadOnlyMemory<char> word)
	{
		word.ThrowIfNull(nameof(word));
		word.ThrowIfEmpty(nameof(word));

		_words.Remove(word.ToString());
	}

	ReadOnlyMemory<char> IWordProvider.GetNextWord()
	{
		if (!HasMoreWords) throw new InvalidOperationException("No more words available.");

		var index = _index++;
		if (index >= _words.Count) throw new InvalidOperationException("No more words available.");

		var word = _words[index];
		return word.AsMemory();
	}
}
