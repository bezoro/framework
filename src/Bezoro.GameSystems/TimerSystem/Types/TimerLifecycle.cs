namespace Bezoro.GameSystems.TimerSystem.Types;

/// <summary>
///     Lifecycle transitions published by <see cref="Services.TimerSystem" />.
/// </summary>
public enum TimerLifecycle : byte
{
	/// <summary>The timer entered the running state from stopped.</summary>
	Started,

	/// <summary>The timer entered the paused state.</summary>
	Paused,

	/// <summary>The timer entered the stopped state.</summary>
	Stopped,

	/// <summary>The timer reached its configured duration.</summary>
	Finished,

	/// <summary>The timer resumed from the paused state.</summary>
	Resumed,

	/// <summary>The timer was explicitly restarted.</summary>
	Restarted
}
