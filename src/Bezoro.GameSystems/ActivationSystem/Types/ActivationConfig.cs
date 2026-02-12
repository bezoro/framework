namespace Bezoro.GameSystems.ActivationSystem.Types;

/// <summary>
///     Configuration resource consumed by activation ECS systems.
/// </summary>
public readonly struct ActivationConfig
{
	/// <summary>
	///     Initializes activation configuration.
	/// </summary>
	/// <param name="maxActivationsPerTick">
	///     Maximum entries to activate per tick. Values less than or equal to zero process all pending entries.
	/// </param>
	/// <param name="callbackDispatcher">
	///     Optional callback dispatcher for marshalling callback execution to a custom context.
	/// </param>
	public ActivationConfig(
		int                          maxActivationsPerTick = int.MaxValue,
		ActivationCallbackDispatcher? callbackDispatcher    = null)
	{
		MaxActivationsPerTick = maxActivationsPerTick;
		CallbackDispatcher = callbackDispatcher;
	}

	/// <summary>
	///     Gets the maximum number of entries to activate per tick.
	/// </summary>
	public int MaxActivationsPerTick { get; }

	/// <summary>
	///     Gets the optional callback dispatcher used by <c>ActivationDispatchSystem</c>.
	/// </summary>
	public ActivationCallbackDispatcher? CallbackDispatcher { get; }
}
