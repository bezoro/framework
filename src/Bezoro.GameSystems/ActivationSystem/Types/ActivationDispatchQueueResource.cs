using System;
using System.Collections.Generic;

namespace Bezoro.GameSystems.ActivationSystem.Types;

/// <summary>
///     Resource queue containing callbacks ready for dispatch.
/// </summary>
public sealed class ActivationDispatchQueueResource
{
	private readonly Queue<Action> _callbacks = new();

	/// <summary>
	///     Gets the number of queued callbacks.
	/// </summary>
	public int Count => _callbacks.Count;

	/// <summary>
	///     Clears all queued callbacks.
	/// </summary>
	public void Clear() => _callbacks.Clear();

	/// <summary>
	///     Enqueues a callback for dispatch.
	/// </summary>
	/// <param name="callback">Callback to enqueue.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="callback" /> is null.</exception>
	public void Enqueue(Action callback)
	{
		if (callback is null) throw new ArgumentNullException(nameof(callback));
		_callbacks.Enqueue(callback);
	}

	/// <summary>
	///     Attempts to dequeue the next callback.
	/// </summary>
	/// <param name="callback">Dequeued callback when available.</param>
	/// <returns><c>true</c> when a callback was dequeued; otherwise <c>false</c>.</returns>
	public bool TryDequeue(out Action callback)
	{
		if (_callbacks.Count == 0)
		{
			callback = null!;
			return false;
		}

		callback = _callbacks.Dequeue();
		return true;
	}
}
