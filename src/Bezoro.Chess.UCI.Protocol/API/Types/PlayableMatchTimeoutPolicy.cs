namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Controls how elapsed clocks convert into timeout results.
/// </summary>
public enum PlayableMatchTimeoutPolicy
{
	/// <summary>Timeout is automatically adjudicated as a loss for the active side.</summary>
	AutomaticLoss = 0,

	/// <summary>Elapsed clocks are tracked but never convert into terminal timeout results automatically.</summary>
	Ignore
}
