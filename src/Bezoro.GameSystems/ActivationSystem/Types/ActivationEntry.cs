using System;

namespace Bezoro.GameSystems.ActivationSystem.Types;

/// <summary>
///     ECS component representing an activation callback and its processing state.
/// </summary>
public struct ActivationEntry
{
	/// <summary>
	///     Initializes a new activation entry.
	/// </summary>
	/// <param name="handle">Stable handle assigned by the command queue.</param>
	/// <param name="callback">Callback invoked once the entry is activated.</param>
	/// <param name="priority">Activation priority. Higher values activate first.</param>
	/// <param name="state">Current activation state.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="callback" /> is null.</exception>
	public ActivationEntry(
		ActivationHandle handle,
		Action           callback,
		int              priority = 0,
		ActivationState  state    = ActivationState.Pending)
	{
		if (callback is null) throw new ArgumentNullException(nameof(callback));

		Handle = handle;
		Callback = callback;
		Priority = priority;
		State = state;
	}

	/// <summary>
	///     Gets the stable activation handle for this entry.
	/// </summary>
	public ActivationHandle Handle { get; }

	/// <summary>
	///     Gets callback invoked when the entry transitions to <see cref="ActivationState.Activated" />.
	/// </summary>
	public Action Callback { get; }

	/// <summary>
	///     Gets priority used for ordering pending entries.
	/// </summary>
	public int Priority { get; }

	/// <summary>
	///     Gets or sets the current activation state.
	/// </summary>
	public ActivationState State { get; set; }
}
