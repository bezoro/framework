namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Represents an adjudicated match result.
/// </summary>
/// <param name="Reason">Terminal reason.</param>
/// <param name="Winner">Winning side when the result is decisive; otherwise <see langword="null" /> for draws.</param>
public readonly record struct PlayableMatchResult(
	PlayableMatchResultReason Reason,
	char?                     Winner
)
{
	/// <summary>
	///     Gets whether the result represents a terminal state.
	/// </summary>
	public bool IsTerminal => Reason != PlayableMatchResultReason.None;

	/// <summary>
	///     Gets whether the result is a draw.
	/// </summary>
	public bool IsDraw => IsTerminal && Winner is null;
}
