using System;
using Bezoro.GameSystems.ActivationSystem.Types;

namespace Bezoro.GameSystems.ActivationSystem.Abstractions;

/// <summary>
///     Thread-safe producer contract for queueing activation commands from any thread.
/// </summary>
public interface IActivationCommandSink
{
	/// <summary>
	///     Queues a callback to be activated by the ECS activation pipeline.
	/// </summary>
	/// <param name="callback">Callback to invoke when activated.</param>
	/// <param name="priority">Activation priority. Higher values activate first.</param>
	/// <returns>Stable handle used for future cancellation.</returns>
	ActivationHandle Register(Action callback, int priority = 0);

	/// <summary>
	///     Queues cancellation for a previously registered activation handle.
	/// </summary>
	/// <param name="handle">Handle to cancel.</param>
	/// <returns><c>true</c> when cancellation was queued; otherwise <c>false</c>.</returns>
	bool Cancel(ActivationHandle handle);
}
