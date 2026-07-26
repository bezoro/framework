namespace Bezoro.TypingSystem.Abstractions;

/// <summary>Provides atomic consumption of words for typing sessions.</summary>
public interface IWordSource
{
	/// <summary>Attempts to consume the next word.</summary>
	/// <param name="word">The consumed word, or empty memory when exhausted.</param>
	/// <returns><see langword="true" /> when a word was consumed; otherwise <see langword="false" />.</returns>
	bool TryGetNextWord(out ReadOnlyMemory<char> word);
}
