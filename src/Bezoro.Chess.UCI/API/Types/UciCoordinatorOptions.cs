using Bezoro.Chess.UCI.Protocol.API.Types;

namespace Bezoro.Chess.UCI.API.Types;

/// <summary>
///     Configuration options for the UCI Coordinator.
/// </summary>
public readonly record struct UciCoordinatorOptions(
	int                                 PonderThreads                  = 2,
	int                                 MultiPv                        = 1,
	uint                                ClassificationDepth            = 6,
	int                                 EngineMoveTimeMs               = 1_000,
	PlayableMatchTimeControl?           TimeControl                    = null,
	PlayableMatchClaimableDrawPolicy    ClaimableDrawPolicy            = PlayableMatchClaimableDrawPolicy.Automatic,
	PlayableMatchDrawOfferPolicy        DrawOfferPolicy                = PlayableMatchDrawOfferPolicy.ExpireOnMove,
	PlayableMatchControlledMoveFallbackPolicy ControlledMoveFallbackPolicy = PlayableMatchControlledMoveFallbackPolicy.UseLocalFallback
)
{
	/// <summary>
	///     Gets the default configuration options.
	///     Use this instead of default(UciCoordinatorOptions) or new UciCoordinatorOptions()
	///     which would initialize all fields to zero.
	/// </summary>
	public static UciCoordinatorOptions Default { get; } = new(2);
}
