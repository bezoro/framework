using System.Reflection;
using Bezoro.TypingSystem.Abstractions;
using Bezoro.TypingSystem.Types;
using FluentAssertions;

namespace Bezoro.TypingSystem.Tests.Types;

public class WordProviderCompatibilityTests
{
	[Fact]
	public void AddWordsFromFile_WhenCalledOnArrayWordProvider_ShouldForwardToFileAdapter()
	{
		string filePath = Path.GetTempFileName();
		File.WriteAllLines(filePath, ["two", "three"]);

		try
		{
			var provider = new ArrayWordProvider(["one"]);
#pragma warning disable CS0618
			provider.AddWordsFromFile(filePath);
#pragma warning restore CS0618

			Drain(provider).Should().Equal("one", "two", "three");
		}
		finally
		{
			File.Delete(filePath);
		}
	}

	[Fact]
	public void AddWordsFromFile_WhenInspectedOnArrayWordProvider_ShouldHaveExactObsoleteMessage()
	{
		var member = typeof(ArrayWordProvider).GetMethod(nameof(ArrayWordProvider.AddWordsFromFile));

		var attribute = member!.GetCustomAttribute<ObsoleteAttribute>();

		attribute.Should().NotBeNull();
		attribute!.Message.Should().Be("Use WordProviderFileExtensions.LoadWordsFromFile instead.");
	}

	[Fact]
	public void TryGetNextWord_WhenLegacyImplementerHasOnlyOldMembers_ShouldUseDefaultBridge()
	{
		IWordSource source = new LegacyWordProvider(["one"]);

		source.TryGetNextWord(out var first).Should().BeTrue();
		source.TryGetNextWord(out var exhausted).Should().BeFalse();

		first.ToString().Should().Be("one");
		exhausted.Should().Be(ReadOnlyMemory<char>.Empty);
	}

	[Fact]
	public void HasMoreWords_WhenProviderIsExhausted_ShouldMatchConsumptionState()
	{
#pragma warning disable CS0618
		IWordProvider provider = new ArrayWordProvider(["one"]);
		var beforeConsumption = provider.HasMoreWords;
		_ = provider.GetNextWord();
		var afterConsumption = provider.HasMoreWords;
#pragma warning restore CS0618

		beforeConsumption.Should().BeTrue();
		afterConsumption.Should().BeFalse();
	}

	[Fact]
	public void GetNextWord_WhenProviderIsExhausted_ShouldPreserveExceptionContract()
	{
#pragma warning disable CS0618
		IWordProvider provider = new ArrayWordProvider(["one"]);
		var word = provider.GetNextWord();
		Action action = () => _ = provider.GetNextWord();
#pragma warning restore CS0618

		word.ToString().Should().Be("one");
		action.Should().Throw<InvalidOperationException>()
			.WithMessage("No more words available.");
	}

	[Fact]
	public void MutationMembers_WhenCalledThroughOldInterface_ShouldMutateConcreteProvider()
	{
		string filePath = Path.GetTempFileName();
		File.WriteAllLines(filePath, ["four"]);

		try
		{
			var concreteProvider = new ArrayWordProvider(["one"]);
#pragma warning disable CS0618
			IWordProvider provider = concreteProvider;
			provider.AddWord("two".AsMemory());
			provider.AddWords(["three".AsMemory()]);
			provider.AddWordsFromFile(filePath);
			provider.RemoveWord("two".AsMemory());
#pragma warning restore CS0618

			concreteProvider.WordCount.Should().Be(3);

#pragma warning disable CS0618
			provider.ClearWords();
#pragma warning restore CS0618

			concreteProvider.WordCount.Should().Be(0);
			concreteProvider.TryGetNextWord(out var word).Should().BeFalse();
			word.Should().Be(ReadOnlyMemory<char>.Empty);
		}
		finally
		{
			File.Delete(filePath);
		}
	}

	[Theory]
	[InlineData("HasMoreWords", "Use TryGetNextWord(out ReadOnlyMemory<char>) instead.")]
	[InlineData("GetNextWord", "Use TryGetNextWord(out ReadOnlyMemory<char>) instead.")]
	[InlineData("WordCount", "Use ArrayWordProvider.WordCount instead.")]
	[InlineData("AddWord", "Use ArrayWordProvider.AddWord instead.")]
	[InlineData("AddWords", "Use ArrayWordProvider.AddWords instead.")]
	[InlineData("AddWordsFromFile", "Use WordProviderFileExtensions.LoadWordsFromFile instead.")]
	[InlineData("ClearWords", "Use ArrayWordProvider.ClearWords instead.")]
	[InlineData("RemoveWord", "Use ArrayWordProvider.RemoveWord instead.")]
	public void OldMember_WhenInspected_ShouldHaveExactObsoleteMessage(string memberName, string message)
	{
		var member = typeof(IWordProvider).GetMember(memberName, BindingFlags.Instance | BindingFlags.Public).Single();

		var attribute = member.GetCustomAttribute<ObsoleteAttribute>();

		attribute.Should().NotBeNull();
		attribute!.Message.Should().Be(message);
	}

	private sealed class LegacyWordProvider(IEnumerable<string> words) : IWordProvider
	{
		private readonly List<string> _words = [.. words];
		private          int          _index;

		public bool HasMoreWords => _index < _words.Count;

		public uint WordCount => (uint)_words.Count;

		public ReadOnlyMemory<char> GetNextWord()
		{
			if (!HasMoreWords) throw new InvalidOperationException("No more words available.");

			return _words[_index++].AsMemory();
		}

		public void AddWord(ReadOnlyMemory<char> word)
		{
			_words.Add(word.ToString());
		}

		public void AddWords(IEnumerable<ReadOnlyMemory<char>> wordsToAdd)
		{
			_words.AddRange(wordsToAdd.Select(static word => word.ToString()));
		}

		public void AddWordsFromFile(string filePath)
		{
			_words.AddRange(File.ReadAllLines(filePath));
		}

		public void ClearWords()
		{
			_words.Clear();
			_index = 0;
		}

		public void RemoveWord(ReadOnlyMemory<char> word)
		{
			_words.Remove(word.ToString());
		}
	}

	private static IEnumerable<string> Drain(IWordSource source)
	{
		while (source.TryGetNextWord(out var word)) yield return word.ToString();
	}
}
