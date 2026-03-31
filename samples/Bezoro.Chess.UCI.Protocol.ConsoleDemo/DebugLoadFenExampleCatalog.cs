namespace Bezoro.Chess.UCI.Protocol.ConsoleDemo;

internal static class DebugLoadFenExampleCatalog
{
	public static IReadOnlyList<DebugLoadFenExample> Examples { get; } =
	[
		new("Mate in 1", "loadfen 7k/5Q2/7K/8/8/8/8/8 w - - 0 1"),
		new("Stalemate", "loadfen k7/1QK5/8/8/8/8/8/8 w - - 0 1"),
		new("En passant", "loadfen 7k/8/8/3pP3/8/8/8/K7 w - d6 0 1"),
		new("Promotion", "loadfen 1r5k/P7/8/8/8/8/8/K7 w - - 0 1"),
		new("Castling", "loadfen r3k2r/8/8/8/8/8/8/R3K2R w KQkq - 0 1")
	];
}
