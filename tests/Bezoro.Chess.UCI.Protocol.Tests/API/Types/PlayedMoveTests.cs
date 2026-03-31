using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.Chess.UCI.Protocol.Tests.API.Types;

[TestSubject(typeof(PlayedMove))]
public class PlayedMoveTests
{
	[Fact]
	public void Constructor_WhenCreated_ShouldPreserveMoveContext()
	{
		var move = new PlayedMove(2, 'b', "c7c5", "fen-parent", "fen-child");

		move.Ply.Should().Be(2);
		move.Side.Should().Be('b');
		move.Move.Should().Be("c7c5");
		move.ParentPositionKey.Should().Be("fen-parent");
		move.PositionKey.Should().Be("fen-child");
	}
}
