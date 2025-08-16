using Bezoro.UCI.API.Types;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.UCI.Tests.Integration.API.Types;

[TestSubject(typeof(BoardState))]
public class BoardStateIntegrationTests
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
		var defaultFen = Fen.Default();

		var board = BoardState.FromFen(defaultFen);

		board.Should().NotBeNull();
	}
}
