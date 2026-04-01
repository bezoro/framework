namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Controls whether claimable draw conditions are adjudicated automatically or require an explicit claim request.
/// </summary>
public enum PlayableMatchClaimableDrawPolicy
{
	/// <summary>Claimable draws are adjudicated automatically as soon as they are available.</summary>
	Automatic = 0,

	/// <summary>Claimable draws remain pending until a caller explicitly claims them.</summary>
	ClaimRequired
}
