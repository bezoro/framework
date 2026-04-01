using System.Collections.Immutable;

namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Serializable request DTO for driving a playable match session from a server or transport boundary.
/// </summary>
/// <param name="Kind">Request kind.</param>
/// <param name="Move">Move notation when applicable.</param>
/// <param name="PromotionPiece">Lowercase promotion suffix when applicable.</param>
/// <param name="BaseFen">Base position when applicable.</param>
/// <param name="Moves">Optional played-move suffix following <paramref name="BaseFen" />.</param>
/// <param name="UndoCount">Number of moves to undo when applicable.</param>
public readonly record struct PlayableMatchRequest(
	PlayableMatchRequestKind Kind,
	string?                  Move           = null,
	char?                    PromotionPiece = null,
	Fen?                     BaseFen        = null,
	ImmutableArray<string>   Moves          = default,
	int                      UndoCount      = 1
)
{
	/// <summary>
	///     Gets the request schema version.
	/// </summary>
	public int SchemaVersion { get; init; } = 1;
}
