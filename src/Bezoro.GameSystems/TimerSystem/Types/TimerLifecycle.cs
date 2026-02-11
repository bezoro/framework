namespace Bezoro.GameSystems.TimerSystem.Types;

/// <summary>
///     Lifecycle transitions published by <see cref="Services.TimerSystem" />.
/// </summary>
public enum TimerLifecycle : byte
{
	Started,
	Paused,
	Stopped,
	Finished,
	Resumed,
	Restarted
}
