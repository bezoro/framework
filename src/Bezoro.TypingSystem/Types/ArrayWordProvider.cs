using System;
using System.Collections.Generic;
using System.IO;
using Bezoro.Core.Extensions;
using Bezoro.Core.Types;
using Bezoro.TypingSystem.Abstractions;

namespace Bezoro.TypingSystem.Types;

/// <summary>
///     A word provider that uses an internal array (via <see cref="SwapbackArray{T}"/>) to store words.
/// </summary>
public sealed class ArrayWordProvider : IWordProvider
{
	private readonly SwapbackArray<string> _words;
	private          uint                  _index;

	/// <summary>
	///     Initializes a new instance of the <see cref="ArrayWordProvider"/> class with the specified words.
	/// </summary>
	/// <param name="words">The collection of words to initialize the provider with.</param>
	public ArrayWordProvider(IEnumerable<string> words)
	{
		words.ThrowIfNull(nameof(words));
		words.ThrowIfEmpty();

		_words = new(words);
	}

	/// <inheritdoc />
	public bool HasMoreWords => _index < _words.Count;

	/// <inheritdoc />
	public uint WordCount => _words.Count;

	/// <inheritdoc />
	public void AddWord(ReadOnlyMemory<char> word)
	{
		_words.Add(word.ToString());
	}

	/// <inheritdoc />
	public void AddWords(IEnumerable<ReadOnlyMemory<char>> words)
	{
		foreach (var word in words) _words.Add(word.ToString());
	}

	/// <inheritdoc />
	public void AddWordsFromFile(string filePath)
	{
		foreach (string? word in File.ReadAllLines(filePath)) _words.Add(word);
	}

	/// <inheritdoc />
	public void ClearWords()
	{
		_words.Clear();
		_index = 0;
	}

	/// <inheritdoc />
	public void RemoveWord(ReadOnlyMemory<char> word)
	{
		word.ThrowIfNull(nameof(word));
		word.ThrowIfEmpty();

		_words.Remove(word.ToString());
	}

	ReadOnlyMemory<char> IWordProvider.GetNextWord()
	{
		if (!HasMoreWords) throw new InvalidOperationException("No more words available.");

		uint index = _index++;
		if (index >= _words.Count) throw new InvalidOperationException("No more words available.");

		string word = _words[index];
		return word.AsMemory();
	}
}
