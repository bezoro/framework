namespace Bezoro.Chess.API.Types;

/// <summary>
///     Represents the type of opponent in a chess game.
/// </summary>
public enum OpponentType
{
	/// <summary>Playing against a chess engine (AI).</summary>
	Engine,

	/// <summary>Playing against a human on the same device.</summary>
	LocalHuman,

	/// <summary>Playing against a human online.</summary>
	RemoteHuman
}

