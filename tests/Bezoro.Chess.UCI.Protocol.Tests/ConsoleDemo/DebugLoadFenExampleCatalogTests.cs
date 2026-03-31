using Bezoro.Chess.UCI.Protocol.ConsoleDemo;
using FluentAssertions;
using JetBrains.Annotations;

namespace Bezoro.Chess.UCI.Protocol.Tests.ConsoleDemo;

[TestSubject(typeof(DebugLoadFenExampleCatalog))]
public class DebugLoadFenExampleCatalogTests
{
	[Fact]
	public void Examples_WhenEnumerated_ShouldAllParseAsLoadFenCommandsWithBothKingsPresent()
	{
		foreach (var example in DebugLoadFenExampleCatalog.Examples)
		{
			var command = PlayableMatchCommandParser.Parse(example.Command);

			command.Kind.Should().Be(PlayableMatchCommandKind.LoadFen, example.Label);
			command.Fen.Should().NotBeNull(example.Label);
			command.Fen!.Value.PiecePlacement.Should().Contain("K", example.Label);
			command.Fen!.Value.PiecePlacement.Should().Contain("k", example.Label);
		}
	}
}
