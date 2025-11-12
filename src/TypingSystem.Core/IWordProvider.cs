using System;

namespace TypingSystem.Core;

public interface IWordProvider
{
	bool HasMoreWords { get; }

	ReadOnlyMemory<char> GetNextWord();
}
