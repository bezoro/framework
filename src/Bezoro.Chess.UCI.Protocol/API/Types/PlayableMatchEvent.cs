namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Serializable canonical event payload emitted by <see cref="UciPlayableMatchSession" />.
/// </summary>
/// <param name="Kind">Event kind.</param>
/// <param name="TimestampUtc">Emission timestamp.</param>
/// <param name="State">Resulting match state when available.</param>
/// <param name="Move">Move notation associated with the event, when applicable.</param>
/// <param name="MoveData">Canonical move payload associated with the event, when applicable.</param>
/// <param name="Result">Adjudicated result associated with the event, when applicable.</param>
/// <param name="PendingPromotion">Pending promotion request when applicable.</param>
/// <param name="UndoCount">Number of moves undone when applicable.</param>
/// <param name="Error">Validation or rejection message when applicable.</param>
public readonly record struct PlayableMatchEvent(
	PlayableMatchEventKind Kind,
	DateTimeOffset         TimestampUtc,
	PlayableMatchState?    State            = null,
	string?                Move             = null,
	PlayableMatchMoveData? MoveData         = null,
	PlayableMatchResult?   Result           = null,
	PendingPromotionRequest? PendingPromotion = null,
	int?                   UndoCount        = null,
	string?                Error            = null
)
{
	/// <summary>
	///     Gets the event schema version for transport compatibility.
	/// </summary>
	public int SchemaVersion { get; init; } = 1;
}
