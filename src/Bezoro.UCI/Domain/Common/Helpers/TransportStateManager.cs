using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Bezoro.UCI.Domain.Common.Helpers;

/// <summary>
///     Manages the state of a transport.
/// </summary>
internal sealed class TransportStateManager
{
	private int _disposed;
	private int _exitedRaised;
	private int _processAlive;
	private int _readerActive;
	private int _status;

	/// <summary>
	///     Gets whether the exited event has been raised.
	/// </summary>
	public bool HasExitedRaised => Volatile.Read(ref _exitedRaised) == 1;

	/// <summary>
	///     Gets whether the transport is disposed.
	/// </summary>
	public bool IsDisposed => Volatile.Read(ref _disposed) == 1;

	/// <summary>
	///     Gets whether the process is alive.
	/// </summary>
	public bool IsProcessAlive => Volatile.Read(ref _processAlive) == 1;

	/// <summary>
	///     Gets whether a reader is active (for single reader mode).
	/// </summary>
	public bool IsReaderActive => Volatile.Read(ref _readerActive) == 1;

	/// <summary>
	///     Gets whether the transport is started.
	/// </summary>
	public bool IsStarted => Volatile.Read(ref _status) == (int)TransportStatus.Started;

	/// <summary>
	///     Gets the current transport status.
	/// </summary>
	public TransportStatus Status => (TransportStatus)Volatile.Read(ref _status);

	/// <summary>
	///     Attempts to mark as disposed.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool TryMarkDisposed() => Interlocked.Exchange(ref _disposed, 1) == 0;

	/// <summary>
	///     Attempts to mark exited as raised.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool TryMarkExitedRaised() => Interlocked.Exchange(ref _exitedRaised, 1) == 0;

	/// <summary>
	///     Exchanges the status.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public TransportStatus ExchangeStatus(TransportStatus newStatus) =>
		(TransportStatus)Interlocked.Exchange(ref _status, (int)newStatus);

	/// <summary>
	///     Ensures a single reader is active.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void EnsureSingleReader(bool singleReaderMode)
	{
		if (!singleReaderMode) return;

		if (Interlocked.CompareExchange(ref _readerActive, 1, 0) != 0)
			throw new InvalidOperationException("Only a single reader is supported for this transport.");
	}

	/// <summary>
	///     Ensures the transport is in a startable state.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void EnsureStartable()
	{
		int status = Volatile.Read(ref _status);
		if (status is (int)TransportStatus.Created or (int)TransportStatus.Stopped) return;

		Interlocked.Exchange(ref _status, (int)TransportStatus.Failed);
		throw new InvalidOperationException("Transport cannot be started in its current state.");
	}

	/// <summary>
	///     Marks the process as alive.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void MarkProcessAlive(bool alive)
	{
		Volatile.Write(ref _processAlive, alive ? 1 : 0);
	}

	/// <summary>
	///     Releases the reader if in single reader mode.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void ReleaseReaderIfSingle(bool singleReaderMode)
	{
		if (singleReaderMode) Volatile.Write(ref _readerActive, 0);
	}

	/// <summary>
	///     Resets the exited raised flag.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void ResetExitedRaised()
	{
		Volatile.Write(ref _exitedRaised, 0);
	}

	/// <summary>
	///     Resets the status if needed after a failure.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void ResetStatusIfNeeded()
	{
		if (Volatile.Read(ref _disposed) == 0 && Volatile.Read(ref _status) != (int)TransportStatus.Failed)
			Volatile.Write(ref _status, (int)TransportStatus.Stopped);
	}

	/// <summary>
	///     Sets the status.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void SetStatus(TransportStatus status)
	{
		Volatile.Write(ref _status, (int)status);
	}

	/// <summary>
	///     Throws if disposed.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void ThrowIfDisposed(string typeName)
	{
		if (Volatile.Read(ref _disposed) == 1) throw new ObjectDisposedException(typeName);
	}

	/// <summary>
	///     Throws if process is not alive.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void ThrowIfProcessNotAlive(Process? process)
	{
		if (Volatile.Read(ref _processAlive) == 0 || process is { HasExited: true })
			throw new InvalidOperationException("Engine process has exited.");
	}

	/// <summary>
	///     Throws if transport is not started.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void ThrowIfProcessNotStarted()
	{
		if (Volatile.Read(ref _status) != (int)TransportStatus.Started)
			throw new InvalidOperationException("Transport is not started.");
	}

	/// <summary>
	///     Gets the status field reference for validation (for compatibility with existing code).
	/// </summary>
	internal int GetStatusField() => Volatile.Read(ref _status);
}
