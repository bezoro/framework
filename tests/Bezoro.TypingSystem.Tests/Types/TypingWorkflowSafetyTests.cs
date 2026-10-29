using System.Collections;
using Bezoro.TypingSystem.Types;
using Xunit;

namespace Bezoro.TypingSystem.Tests.Types;

public class TypingWorkflowSafetyTests
{
	[Theory]
	[InlineData(1)]
	[InlineData(5)]
	[InlineData(255)]
	public void CorrectInputs_FromInitialState_CompleteTheWord(int length)
	{
		var state = TypingState.Initial;
		for (var i = 0; i < length; i++)
		{
			Assert.False(state.IsComplete((byte)length));
			state = state.WithCorrect();
		}
		Assert.True(state.IsComplete((byte)length));
	}

	[Theory]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(3)]
	public void RemovingConsumedWord_DoesNotSkipUnreadWordsOrRepeatConsumedWords(int consumed)
	{
		var provider = new ArrayWordProvider(new[] { "one", "two", "three", "four" });
		for (var i = 0; i < consumed; i++) Assert.True(provider.TryGetNextWord(out _));
		provider.RemoveWord("one".AsMemory());

		var unread = new List<string>();
		while (provider.TryGetNextWord(out var word)) unread.Add(word.ToString());
		Assert.Equal(new[] { "one", "two", "three", "four" }.Skip(consumed).Order(), unread.Order());
	}

	[Fact]
	public void Constructor_ConsumesSourceOnceWithoutDiscardingFirstWord()
	{
		var source = new SinglePassWords();
		var provider = new ArrayWordProvider(source);
		Assert.Equal(1, source.Enumerations);
		Assert.True(provider.TryGetNextWord(out var first));
		Assert.Equal("one", first.ToString());
		Assert.True(provider.TryGetNextWord(out var second));
		Assert.Equal("two", second.ToString());
	}

	private sealed class SinglePassWords : IEnumerable<string>
	{
		public int Enumerations { get; private set; }
		public IEnumerator<string> GetEnumerator()
		{
			if (++Enumerations > 1) throw new InvalidOperationException("Source can only be consumed once.");
			yield return "one";
			yield return "two";
		}
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}
}
