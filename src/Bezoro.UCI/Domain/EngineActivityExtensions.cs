namespace Bezoro.UCI.Domain;

/// <summary>
///     Extension methods for <see cref="EngineActivity" /> to provide convenient state checks.
/// </summary>
public static class EngineActivityExtensions
{
	/// <summary>
	///     Returns true if the engine is actively searching or pondering (not idle).
	/// </summary>
	public static bool IsActive(this EngineActivity activity) =>
		activity is EngineActivity.Searching or EngineActivity.Pondering;

	/// <summary>
	///     Returns true if the engine is idle (not searching or pondering).
	/// </summary>
	public static bool IsIdle(this EngineActivity activity) => activity == EngineActivity.Idle;

	/// <summary>
	///     Returns true if the engine is currently pondering (not searching or idle).
	/// </summary>
	public static bool IsPondering(this EngineActivity activity) => activity == EngineActivity.Pondering;

	/// <summary>
	///     Returns true if the engine is currently searching (not pondering or idle).
	/// </summary>
	public static bool IsSearching(this EngineActivity activity) => activity == EngineActivity.Searching;
}
