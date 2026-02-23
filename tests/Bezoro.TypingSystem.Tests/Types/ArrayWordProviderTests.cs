using Bezoro.Core.Types.Exceptions;
using Bezoro.TypingSystem.Abstractions;
using Bezoro.TypingSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.TypingSystem.Tests.Types;

[TestSubject(typeof(ArrayWordProvider))]
public class ArrayWordProviderTests
{
	[Fact]
	public void Constructor_WhenWordsAreNull_ShouldThrowArgumentNullException()
	{
		IEnumerable<string> words = null!;

		Action action = () => _ = new ArrayWordProvider(words);

		action.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public void Constructor_WhenWordsAreEmpty_ShouldThrowEmptyCollectionException()
	{
		Action action = () => _ = new ArrayWordProvider([]);

		action.Should().Throw<EmptyCollectionException>();
	}

	[Fact]
	public void GetNextWord_WhenWordsExist_ShouldReturnWordsInInsertionOrder()
	{
		IWordProvider provider = new ArrayWordProvider(["one", "two"]);

		var first  = provider.GetNextWord();
		var second = provider.GetNextWord();

		first.ToString().Should().Be("one");
		second.ToString().Should().Be("two");
	}

	[Fact]
	public void GetNextWord_WhenNoWordsRemain_ShouldThrowInvalidOperationException()
	{
		IWordProvider provider = new ArrayWordProvider(["one"]);
		_ = provider.GetNextWord();

		Action action = () => _ = provider.GetNextWord();

		action.Should().Throw<InvalidOperationException>();
	}

	[Fact]
	public void HasMoreWords_WhenAllWordsAreConsumed_ShouldReturnFalse()
	{
		IWordProvider provider = new ArrayWordProvider(["one"]);
		_ = provider.GetNextWord();

		provider.HasMoreWords.Should().BeFalse();
	}

	[Fact]
	public void AddWord_WhenCalled_ShouldIncreaseWordCount()
	{
		var provider = new ArrayWordProvider(["one"]);

		provider.AddWord("two".AsMemory());

		provider.WordCount.Should().Be(2);
	}

	[Fact]
	public void AddWords_WhenCalled_ShouldAddAllWords()
	{
		var provider = new ArrayWordProvider(["one"]);

		provider.AddWords(["two".AsMemory(), "three".AsMemory()]);

		provider.WordCount.Should().Be(3);
	}

	[Fact]
	public void WordCount_WhenWordsAreAddedAndRemoved_ShouldReflectCurrentCount()
	{
		var provider = new ArrayWordProvider(["one", "two"]);
		provider.AddWord("three".AsMemory());
		provider.RemoveWord("two".AsMemory());

		provider.WordCount.Should().Be(2);
	}

	[Fact]
	public void RemoveWord_WhenWordDoesNotExist_ShouldThrowInvalidOperationException()
	{
		var provider = new ArrayWordProvider(["one"]);

		Action action = () => provider.RemoveWord("missing".AsMemory());

		action.Should().Throw<InvalidOperationException>();
	}

	[Fact]
	public void ClearWords_WhenCalled_ShouldResetWordCountAndReadIndex()
	{
		IWordProvider provider = new ArrayWordProvider(["one", "two"]);
		_ = provider.GetNextWord();
		provider.ClearWords();
		provider.AddWord("three".AsMemory());

		var word = provider.GetNextWord();

		word.ToString().Should().Be("three");
		provider.WordCount.Should().Be(1);
		provider.HasMoreWords.Should().BeFalse();
	}

	[Fact]
	public void AddWordsFromFile_WhenFileContainsWords_ShouldAppendWords()
	{
		var filePath = Path.GetTempFileName();
		File.WriteAllLines(filePath, ["two", "three"]);

		try
		{
			IWordProvider provider = new ArrayWordProvider(["one"]);

			provider.AddWordsFromFile(filePath);

			provider.WordCount.Should().Be(3);
			provider.GetNextWord().ToString().Should().Be("one");
			provider.GetNextWord().ToString().Should().Be("two");
			provider.GetNextWord().ToString().Should().Be("three");
		}
		finally
		{
			File.Delete(filePath);
		}
	}
}
