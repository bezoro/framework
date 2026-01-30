namespace Bezoro.GameSystems.TimerSystem.Types;

/// <summary>
///     Represents the current state of a timer.
/// </summary>
public enum TimerState : byte
{
	/// <summary>The timer is actively counting down.</summary>
	Running,

	/// <summary>The timer is paused; elapsed time is preserved.</summary>
	Paused,

	/// <summary>The timer was cancelled before completion.</summary>
	Stopped,

	/// <summary>The timer has reached its duration and fired its callback.</summary>
	Completed
}
