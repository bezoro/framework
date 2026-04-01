using System.Collections.Immutable;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.Chess.UCI.Protocol.Tests.ConsoleDemo;

[TestSubject(typeof(PlayableMatchCommandParser))]
public class PlayableConsoleCommandParserTests
{
	[Fact]
	public void Parse_WhenHistoryCommandIsProvided_ShouldReturnHistory()
	{
		var command = PlayableMatchCommandParser.Parse("history");

		command.Kind.Should().Be(PlayableMatchCommandKind.History);
	}

	[Fact]
	public void Parse_WhenLoadFenIsInvalid_ShouldReturnInvalidWithMessage()
	{
		var command = PlayableMatchCommandParser.Parse("loadfen not-a-fen");

		command.Kind.Should().Be(PlayableMatchCommandKind.Invalid);
		command.Error.Should().Contain("FEN");
	}

	[Fact]
	public void Parse_WhenLoadFenWithOptionalMovesIsProvided_ShouldReturnLoadFen()
	{
		var command = PlayableMatchCommandParser.Parse(
			"loadfen rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1 moves e2e4 e7e5"
		);

		command.Kind.Should().Be(PlayableMatchCommandKind.LoadFen);
		command.Fen.Should().NotBeNull();
		command.Fen!.Value.ActiveColor.Should().Be('w');
		command.Moves.Should().Equal(ImmutableArray.Create("e2e4", "e7e5"));
	}

	[Fact]
	public void Parse_WhenMoveCommandIsProvided_ShouldReturnMove()
	{
		var command = PlayableMatchCommandParser.Parse("e2e4");

		command.Kind.Should().Be(PlayableMatchCommandKind.Move);
		command.Move.Should().Be("e2e4");
	}

	[Fact]
	public void Parse_WhenUndoCommandIsProvided_ShouldReturnUndo()
	{
		var command = PlayableMatchCommandParser.Parse("undo");

		command.Kind.Should().Be(PlayableMatchCommandKind.Undo);
	}

	[Fact]
	public void Parse_WhenQuitCommandIsProvided_ShouldReturnQuit()
	{
		var command = PlayableMatchCommandParser.Parse("quit");

		command.Kind.Should().Be(PlayableMatchCommandKind.Quit);
	}
}
