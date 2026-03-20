using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.Chess.UCI.Protocol.Tests.Domain;

[TestSubject(typeof(UciEngineClient))]
public class UciEngineClientIsUciMoveStringTests
{
	[Theory]
	[InlineData("e9e4",  false, "e9 is an invalid square")]
	[InlineData("i2e4",  false, "i2 is an invalid square")]
	[InlineData("e2e",   false, "incomplete move notation")]
	[InlineData("e2e45", false, "rank out of bounds")]
	[InlineData("e2e4x", false, "extra characters")]
	[InlineData("e2e4",  true,  "e2e4 is a valid UCI move")]
	[InlineData("a7a8q", true,  "a7a8q is a valid promotion move")]
	[InlineData("H7H8N", true,  "H7H8N is a valid move (case insensitive)")]
	public void IsUciMoveString_WhenEvaluatingVariousInputs_ShouldReturnExpectedResult(
		string move,
		bool   expected,
		string message)
	{
		UciEngineClient.IsUciMoveString(move).Should().Be(expected, message);
	}
}
