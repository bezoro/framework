using Bezoro.Chess.UCI.Protocol.API.Types;
using Bezoro.Chess.UCI.Protocol.ConsoleDemo;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.Chess.UCI.Protocol.Tests.ConsoleDemo;

[TestSubject(typeof(PlayableTurnCommandRouter))]
public class PlayableTurnCommandRouterTests
{
	[Theory]
	[InlineData(PlayableMatchCommandKind.Moves, true)]
	[InlineData(PlayableMatchCommandKind.History, true)]
	[InlineData(PlayableMatchCommandKind.Undo, false)]
	[InlineData(PlayableMatchCommandKind.Move, false)]
	[InlineData(PlayableMatchCommandKind.LoadFen, false)]
	[InlineData(PlayableMatchCommandKind.Quit, false)]
	public void ShouldHandleInsidePrompt_WhenCommandKindIsEvaluated_ShouldReturnExpectedValue(
		PlayableMatchCommandKind commandKind,
		bool expected)
	{
		PlayableTurnCommandRouter.ShouldHandleInsidePrompt(commandKind).Should().Be(expected);
	}

	[Theory]
	[InlineData(PlayableMatchCommandKind.Move, true)]
	[InlineData(PlayableMatchCommandKind.Invalid, true)]
	[InlineData(PlayableMatchCommandKind.Moves, false)]
	[InlineData(PlayableMatchCommandKind.History, false)]
	[InlineData(PlayableMatchCommandKind.Undo, false)]
	[InlineData(PlayableMatchCommandKind.LoadFen, false)]
	[InlineData(PlayableMatchCommandKind.Quit, false)]
	public void ShouldValidateAsMove_WhenCommandKindIsEvaluated_ShouldReturnExpectedValue(
		PlayableMatchCommandKind commandKind,
		bool expected)
	{
		PlayableTurnCommandRouter.ShouldValidateAsMove(commandKind).Should().Be(expected);
	}
}
