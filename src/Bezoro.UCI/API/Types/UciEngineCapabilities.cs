namespace Bezoro.UCI.API.Types;

/// <summary>
///     Describes the standard and extension capabilities available on the current engine.
/// </summary>
public readonly record struct UciEngineCapabilities(
	UciCapabilityState DebugCommand,
	UciCapabilityState RegisterCommand,
	UciCapabilityState PonderHit,
	UciCapabilityState DisplayBoardFen,
	UciCapabilityState PerftMoveListing
)
{
	/// <summary>
	///     Gets a value with every capability set to <see cref="UciCapabilityState.Unknown" />.
	/// </summary>
	public static UciEngineCapabilities Unknown { get; } = new(
		UciCapabilityState.Unknown,
		UciCapabilityState.Unknown,
		UciCapabilityState.Unknown,
		UciCapabilityState.Unknown,
		UciCapabilityState.Unknown
	);

	/// <summary>
	///     Gets a value indicating whether the extensions required by <see cref="Bezoro.UCI.API.UciCoordinator" />
	///     are available.
	/// </summary>
	public bool SupportsCoordinatorExtensions =>
		DisplayBoardFen == UciCapabilityState.Supported &&
		PerftMoveListing == UciCapabilityState.Supported;
}
