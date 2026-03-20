using System.Threading;

namespace Bezoro.Chess.UCI.Protocol.Domain.EngineClient;

/// <summary>
///     Tracks engine activity transitions and publishes notifications.
/// </summary>
internal sealed class EngineActivityTracker(EngineActivity initial = EngineActivity.Idle)
{
	private int _activity = (int)initial;

	/// <summary>
	///     Occurs when the activity state changes.
	/// </summary>
	public event Action<EngineActivity, EngineActivity>? ActivityChanged;

	/// <summary>
	///     Current activity state.
	/// </summary>
	public EngineActivity Current => (EngineActivity)Volatile.Read(ref _activity);

	/// <summary>
	///     Atomically updates the activity state and raises <see cref="ActivityChanged" /> if the state changed.
	/// </summary>
	public void Set(EngineActivity next)
	{
		var previous = (EngineActivity)Interlocked.Exchange(ref _activity, (int)next);
		if (previous == next) return;

		try
		{
			ActivityChanged?.Invoke(previous, next);
		}
		catch
		{
			/* swallow */
		}
	}
}
