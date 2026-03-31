using Bezoro.Chess.UCI.Protocol.API.Common.Extensions;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.Chess.UCI.Protocol.Tests.API.Common.Extensions;

[TestSubject(typeof(FenExtensions))]
public class FenExtensionsTests
{
	[Fact]
	public void ToDisplayLines_WhenViewingFromBlackPerspective_ShouldReverseFilesAndRanks()
	{
		var fen = Fen.Parse("8/8/8/3k4/8/4K3/8/8 b - - 0 1")!.Value;

		string[] lines = fen.ToDisplayLines(playerColor: 'b', legalMoveCount: 8);

		lines[0].Should().Be("Move 1 | Black to move | 8 legal moves");
		lines[1].Should().Be("  h g f e d c b a");
		lines.Should().Contain("5 . . . . k . . . 5");
		lines.Should().Contain("3 . . . K . . . . 3");
		lines[^1].Should().Be("  h g f e d c b a");
	}
}
