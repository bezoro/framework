namespace Bezoro.GameSystems.TimerSystem.Types;

/// <summary>
///     Determines a timer's lifecycle behavior after completion.
/// </summary>
public enum TimerMode
{
	/// <summary>Auto-removed from storage after the completion callback fires.</summary>
	OneShot,

	/// <summary>Stays in storage after completion for reuse via <c>Restart()</c>.</summary>
	Persistent
}
