namespace Bezoro.GameSystems.TimerSystem.Types;

internal enum TimerPendingAction : byte
{
	None = 0,
	Started = 1,
	Paused = 2,
	Stopped = 3,
	Resumed = 4,
	Restarted = 5
}
