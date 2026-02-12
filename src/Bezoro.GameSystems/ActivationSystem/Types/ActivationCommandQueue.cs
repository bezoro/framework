using System;
using System.Collections.Concurrent;
using System.Threading;
using Bezoro.GameSystems.ActivationSystem.Abstractions;

namespace Bezoro.GameSystems.ActivationSystem.Types;

/// <summary>
///     Thread-safe ingress queue for activation register/cancel commands.
/// </summary>
public sealed class ActivationCommandQueue : IActivationCommandSink
{
	private readonly ConcurrentQueue<ActivationCommand> _pendingCommands = new();
	private          int                                _nextHandleId;

	/// <summary>
	///     Gets an approximate count of pending ingress commands.
	/// </summary>
	public int PendingCommandCount => _pendingCommands.Count;

	/// <inheritdoc />
	public bool Cancel(ActivationHandle handle)
	{
		if (!handle.IsValid)
			return false;

		_pendingCommands.Enqueue(ActivationCommand.Cancel(handle));
		return true;
	}

	/// <inheritdoc />
	public ActivationHandle Register(Action callback, int priority = 0)
	{
		if (callback is null) throw new ArgumentNullException(nameof(callback));

		int id = Interlocked.Increment(ref _nextHandleId);
		var handle = new ActivationHandle(id);
		_pendingCommands.Enqueue(ActivationCommand.Register(handle, callback, priority));
		return handle;
	}

	internal bool TryDequeue(out ActivationCommand command) =>
		_pendingCommands.TryDequeue(out command);
}
