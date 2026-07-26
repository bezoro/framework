using Bezoro.Core.Types.Exceptions;
using Bezoro.TypingSystem.Abstractions;
using Bezoro.TypingSystem.Extensions;
using Bezoro.TypingSystem.Types;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.TypingSystem.Tests.Types;

[TestSubject(typeof(ArrayWordProvider))]
public class ArrayWordProviderTests
{
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
	public void LoadWordsFromFile_WhenFileContainsWords_ShouldAppendWords()
	{
		string filePath = Path.GetTempFileName();
		File.WriteAllLines(filePath, ["two", "three"]);

		try
		{
			var provider = new ArrayWordProvider(["one"]);
			provider.LoadWordsFromFile(filePath);

			provider.WordCount.Should().Be(3);
			Drain(provider).Should().Equal("one", "two", "three");
		}
		finally
		{
			File.Delete(filePath);
		}
	}

	[Fact]
	public void LoadWordsFromFile_WhenProviderIsNull_ShouldThrowArgumentNullException()
	{
		ArrayWordProvider provider = null!;

		var action = () => provider.LoadWordsFromFile("words.txt");

		action.Should().Throw<ArgumentNullException>()
			.WithParameterName("provider");
	}

	[Fact]
	public void LoadWordsFromFile_WhenFilePathIsNull_ShouldThrowArgumentNullException()
	{
		var provider = new ArrayWordProvider(["one"]);

		var action = () => provider.LoadWordsFromFile(null!);

		action.Should().Throw<ArgumentNullException>()
			.WithParameterName("filePath");
	}

	[Fact]
	public void LoadWordsFromFile_WhenFileDoesNotExist_ShouldThrowFileNotFoundException()
	{
		var provider = new ArrayWordProvider(["one"]);
		var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.txt");

		var action = () => provider.LoadWordsFromFile(filePath);

		action.Should().Throw<FileNotFoundException>();
	}

	[Fact]
	public void ClearWords_WhenCalled_ShouldResetWordCountAndReadIndex()
	{
		var provider = new ArrayWordProvider(["one", "two"]);
		_ = provider.TryGetNextWord(out _);
		provider.ClearWords();
		provider.AddWord("three".AsMemory());

		provider.TryGetNextWord(out var word).Should().BeTrue();

		word.ToString().Should().Be("three");
		provider.WordCount.Should().Be(1);
		provider.TryGetNextWord(out _).Should().BeFalse();
	}

	[Fact]
	public void Constructor_WhenWordsAreEmpty_ShouldThrowEmptyCollectionException()
	{
		Action action = () => _ = new ArrayWordProvider([]);

		action.Should().Throw<EmptyCollectionException>();
	}

	[Fact]
	public void Constructor_WhenWordsAreNull_ShouldThrowArgumentNullException()
	{
		IEnumerable<string> words = null!;

		Action action = () => _ = new ArrayWordProvider(words);

		action.Should().Throw<ArgumentNullException>();
	}

	[Fact]
	public void TryGetNextWord_WhenWordsExist_ShouldConsumeInInsertionOrder()
	{
		IWordSource source = new ArrayWordProvider(["one", "two"]);

		source.TryGetNextWord(out var first).Should().BeTrue();
		source.TryGetNextWord(out var second).Should().BeTrue();

		first.ToString().Should().Be("one");
		second.ToString().Should().Be("two");
	}

	[Fact]
	public void TryGetNextWord_WhenExhausted_ShouldReturnFalseAndEmptyMemory()
	{
		IWordSource source = new ArrayWordProvider(["one"]);
		_ = source.TryGetNextWord(out _);

		source.TryGetNextWord(out var word).Should().BeFalse();
		word.Should().Be(ReadOnlyMemory<char>.Empty);
	}

	[Fact]
	public void RemoveWord_WhenWordDoesNotExist_ShouldThrowInvalidOperationException()
	{
		var provider = new ArrayWordProvider(["one"]);

		var action = () => provider.RemoveWord("missing".AsMemory());

		action.Should().Throw<InvalidOperationException>();
	}

	[Fact]
	public void WordCount_WhenWordsAreAddedAndRemoved_ShouldReflectCurrentCount()
	{
		var provider = new ArrayWordProvider(["one", "two"]);
		provider.AddWord("three".AsMemory());
		provider.RemoveWord("two".AsMemory());

		provider.WordCount.Should().Be(2);
	}

	private static IEnumerable<string> Drain(IWordSource source)
	{
		while (source.TryGetNextWord(out var word)) yield return word.ToString();
	}
}
