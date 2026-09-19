using Bezoro.Core.Extensions;
using Bezoro.Core.Types;
using Bezoro.TypingSystem.Abstractions;
using Bezoro.TypingSystem.Extensions;

namespace Bezoro.TypingSystem.Types;

/// <summary>
///     A word provider that uses an internal array (via <see cref="SwapbackArray{T}" />) to store words.
/// </summary>
/// <remarks>
///     Concurrent calls to <see cref="TryGetNextWord" /> consume each stored word at most once.
///     Mutations must be externally synchronized with all other operations on this provider.
/// </remarks>
public sealed class ArrayWordProvider : IWordProvider
{
	private readonly SwapbackArray<string> _words;
	private          int                   _index;

	/// <summary>
	///     Initializes a new instance of the <see cref="ArrayWordProvider" /> class with the specified words.
	/// </summary>
	/// <param name="words">The collection of words to initialize the provider with.</param>
	public ArrayWordProvider(IEnumerable<string> words)
	{
		words.ThrowIfNull();
		words.ThrowIfEmpty();

		_words = new(words);
	}

	/// <inheritdoc />
	[Obsolete("Use TryGetNextWord(out ReadOnlyMemory<char>) instead.")]
	public bool HasMoreWords => Volatile.Read(ref _index) < _words.Count;

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
	[Obsolete("Use WordProviderFileExtensions.LoadWordsFromFile instead.")]
	public void AddWordsFromFile(string filePath) => this.LoadWordsFromFile(filePath);

	/// <inheritdoc />
	public void ClearWords()
	{
		_words.Clear();
		_index = 0;
	}

	/// <inheritdoc />
	public void RemoveWord(ReadOnlyMemory<char> word)
	{
		word.ThrowIfNull();
		word.ThrowIfEmpty();

		_words.Remove(word.ToString());
	}

	/// <inheritdoc />
	public bool TryGetNextWord(out ReadOnlyMemory<char> word)
	{
		while (true)
		{
			var index = Volatile.Read(ref _index);
			if (index >= _words.Count)
			{
				word = ReadOnlyMemory<char>.Empty;
				return false;
			}

			// Failed reads must not advance past words appended after exhaustion.
			if (Interlocked.CompareExchange(ref _index, index + 1, index) != index) continue;

			word = _words[(uint)index].AsMemory();
			return true;
		}
	}

	ReadOnlyMemory<char> IWordProvider.GetNextWord()
	{
		if (!TryGetNextWord(out var word)) throw new InvalidOperationException("No more words available.");

		return word;
	}
}
