namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Describes a parsed playable-match input command.
/// </summary>
public enum PlayableMatchCommandKind
{
	/// <summary>The input could not be parsed.</summary>
	Invalid,

	/// <summary>A legal-move command in UCI notation.</summary>
	Move,

	/// <summary>The caller requested legal-move analysis output.</summary>
	Moves,

	/// <summary>The caller requested played-move history output.</summary>
	History,

	/// <summary>The caller requested undoing recently played moves.</summary>
	Undo,

	/// <summary>The caller requested loading an explicit FEN and optional move sequence.</summary>
	LoadFen,

	/// <summary>The caller requested quitting the match.</summary>
	Quit
}
