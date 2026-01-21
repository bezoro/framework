using Bezoro.UCI.API.Types;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.UCI.Tests.API.Types;

[TestSubject(typeof(BoardState))]
public class BoardStateTests
{
	[Fact]
	public void FromFen_WhenInvalidFen_ShouldThrowArgumentException()
	{
		var invalidFen = Fen.Empty();

		var boardState = BoardState.FromFen(invalidFen);

		boardState.Should().BeNull();
	}

	[Fact]
	public void FromFen_WhenValidFen_ShouldReturnValidObject()
	{
		var board = BoardState.FromFen(Fen.Default);

		board.Should().NotBeNull();
	}
}
