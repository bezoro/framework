namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Represents a move that was played in a match, including the parent and resulting position keys.
/// </summary>
/// <param name="Ply">One-based ply group for display, such as <c>1</c> for white and black's first moves.</param>
/// <param name="Side">The side that played the move: <c>w</c> or <c>b</c>.</param>
/// <param name="Move">Move in lowercase UCI notation.</param>
/// <param name="ParentPositionKey">Stable identifier for the position before the move was played.</param>
/// <param name="PositionKey">Stable identifier for the resulting position after the move was played.</param>
/// <param name="Classification">Best-known move classification for the played move.</param>
public readonly record struct PlayedMove(
	int    Ply,
	char   Side,
	string Move,
	string ParentPositionKey,
	string PositionKey,
	MoveClassification Classification = default
);
