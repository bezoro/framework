namespace Bezoro.GameSystems.ActivationSystem.Types;

/// <summary>
///     Represents the current state of an activation entry.
/// </summary>
public enum ActivationState : byte
{
	/// <summary>The entry is waiting to be activated.</summary>
	Pending,

	/// <summary>The entry has been activated and its callback invoked.</summary>
	Activated,

	/// <summary>The entry was cancelled before activation.</summary>
	Cancelled
}
