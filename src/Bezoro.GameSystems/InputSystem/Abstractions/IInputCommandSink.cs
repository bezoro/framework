using Bezoro.GameSystems.InputSystem.Types;

namespace Bezoro.GameSystems.InputSystem.Abstractions;

/// <summary>
///     Accepts externally produced input commands for later ECS consumption.
/// </summary>
public interface IInputCommandSink
{
	/// <summary>
	///     Enqueues a command produced by an external source.
	/// </summary>
	/// <param name="command">The input command to enqueue.</param>
	void Enqueue(in InputCommand command);
}
