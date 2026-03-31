namespace Bezoro.Chess.UCI.Protocol.ConsoleDemo;

internal readonly record struct MoveHistoryEntry(
	int    Ply,
	char   Side,
	string Move,
	string ParentPositionKey,
	string PositionKey
);
