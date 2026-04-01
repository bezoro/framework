namespace Bezoro.Chess.UCI.API.Common.Enums;

/// <summary>
///     Identifies who initiated a move application in the coordinator.
/// </summary>
public enum GameMoveActor
{
	/// <summary>A human player initiated the move.</summary>
	Human,

	/// <summary>The engine initiated the move.</summary>
	Engine,

	/// <summary>A remote or networked player initiated the move.</summary>
	Remote
}
