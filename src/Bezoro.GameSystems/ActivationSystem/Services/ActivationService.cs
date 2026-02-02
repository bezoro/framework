using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.GameSystems.ActivationSystem.Abstractions;
using Bezoro.GameSystems.ActivationSystem.Types;

namespace Bezoro.GameSystems.ActivationSystem.Services;

/// <summary>
///     A thread-safe activation service that spreads object activation over time
///     using a time-budget approach on a background thread. Callbacks can be marshalled
///     to a specified <see cref="SynchronizationContext" />.
/// </summary>
public sealed class ActivationService : IActivationService
{
	private readonly ConcurrentDictionary<int, ActivationEntry> _entries       = new();
	private readonly ConcurrentQueue<Action>                    _callbackQueue = new();
	private          ActivationConfig                           _config;
	private          CancellationTokenSource?                   _cts;
	private          int                                        _disposed;
	private          int                                        _lastSnapshotCount;
	private          int                                        _nextId;

	// Sorted snapshot for priority ordering
	private int[]?                  _sortedKeys;
	private SynchronizationContext? _syncContext;
	private Task?                   _processingTask;

	/// <inheritdoc />
	public event Action? Completed;

	/// <inheritdoc />
	public bool IsComplete => PendingCount == 0;

	/// <inheritdoc />
	public bool IsRunning => _processingTask is { IsCompleted: false };

	/// <inheritdoc />
	public int ActivatedCount => _entries.Count(kvp => kvp.Value.State == ActivationState.Activated);

	/// <inheritdoc />
	public int PendingCount => _entries.Count(kvp => kvp.Value.State == ActivationState.Pending);

	/// <inheritdoc />
	public ActivationHandle Register(Action callback, int priority = 0)
	{
		ThrowIfDisposed();

		if (callback is null)
			throw new ArgumentNullException(nameof(callback));

		int id     = Interlocked.Increment(ref _nextId);
		var handle = new ActivationHandle(id);
		var entry  = new ActivationEntry(callback, priority, ActivationState.Pending);

		_entries.TryAdd(id, entry);

		return handle;
	}

	/// <inheritdoc />
	public bool Cancel(ActivationHandle handle)
	{
		if (!handle.IsValid) return false;

		var transitioned = false;

		_entries.AddOrUpdate(
			handle.Id,
			_ => default,
			(_, existing) =>
			{
				if (existing.State != ActivationState.Pending)
					return existing;

				existing.State = ActivationState.Cancelled;
				transitioned   = true;
				return existing;
			}
		);

		return transitioned;
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (Interlocked.Exchange(ref _disposed, 1) != 0)
			return;

		Stop();
		_entries.Clear();
	}

	/// <inheritdoc />
	public void Start(ActivationConfig config)
	{
		ThrowIfDisposed();

		if (IsRunning) return;

		_config      = config;
		_syncContext = config.CallbackContext;

		_cts = new();
		var token = _cts.Token;

		_processingTask = Task.Run(() => ProcessingLoopAsync(token), token);
	}

	/// <inheritdoc />
	public void Stop()
	{
		if (!IsRunning)
			return;

		_cts?.Cancel();

		try
		{
			_processingTask?.Wait(TimeSpan.FromSeconds(5));
		}
		catch (AggregateException ex) when (ex.InnerExceptions.Count == 1 &&
											ex.InnerExceptions[0] is TaskCanceledException)
		{
			// Expected during cancellation
		}
		catch (TaskCanceledException)
		{
			// Expected during cancellation
		}

		_cts?.Dispose();
		_cts            = null;
		_processingTask = null;

		while (_callbackQueue.TryDequeue(out _)) { }

		ReturnSortedKeys();
	}

	private async Task ProcessingLoopAsync(CancellationToken ct)
	{
		while (!ct.IsCancellationRequested)
		{
			try
			{
				ProcessBatch();
				FlushCallbacks();
				CheckCompletion();
				await Task.Delay(_config.IterationDelayMs, ct).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				break;
			}
			catch (Exception)
			{
				// Don't let exceptions kill the loop
			}
		}
	}

	private void CheckCompletion()
	{
		if (PendingCount != 0)
			return;

		// Only fire if there are activated entries (meaning work was done)
		if (ActivatedCount == 0)
			return;

		try
		{
			Completed?.Invoke();
		}
		catch
		{
			// Don't let event handler exceptions crash the system
		}
	}

	private void FlushCallbacks()
	{
		if (_callbackQueue.IsEmpty)
			return;

		if (_syncContext is null)
		{
			InvokeCallbacksDirect();
			return;
		}

		_syncContext.Post(_ => InvokeCallbacksDirect(), null);
	}

	private void InvokeCallbacksDirect()
	{
		while (_callbackQueue.TryDequeue(out var callback))
		{
			try
			{
				callback.Invoke();
			}
			catch
			{
				// Don't let callback exceptions crash the system
			}
		}
	}

	private void ProcessBatch()
	{
		RebuildSnapshotIfNeeded();

		int[]? keys = _sortedKeys;
		if (keys is null) return;

		var sw          = Stopwatch.StartNew();
		var budgetTicks = (long)(_config.TimeBudgetMs * Stopwatch.Frequency / 1000.0);
		var activated   = 0;

		for (var i = 0; i < keys.Length; i++)
		{
			int key = keys[i];

			if (!_entries.TryGetValue(key, out var entry))
				continue;

			if (entry.State != ActivationState.Pending)
				continue;

			// Activate: transition state and enqueue callback
			var callback = entry.Callback;

			_entries.AddOrUpdate(
				key,
				_ => default,
				(_, existing) =>
				{
					if (existing.State != ActivationState.Pending)
						return existing;

					existing.State = ActivationState.Activated;
					return existing;
				}
			);

			_callbackQueue.Enqueue(callback);
			activated++;

			if (activated >= _config.MaxBatchSize)
				break;

			if (activated >= _config.MinBatchSize && sw.ElapsedTicks >= budgetTicks)
				break;
		}
	}

	private void RebuildSnapshotIfNeeded()
	{
		int currentCount = _entries.Count;

		// Only rebuild when the dictionary count changes
		if (_sortedKeys is { } && currentCount == _lastSnapshotCount)
			return;

		ReturnSortedKeys();

		if (currentCount == 0)
		{
			_lastSnapshotCount = 0;
			return;
		}

		int[] pendingEntries = _entries
							   .Where(kvp => kvp.Value.State == ActivationState.Pending)
							   .OrderByDescending(kvp => kvp.Value.Priority)
							   .ThenBy(kvp => kvp.Key)
							   .Select(kvp => kvp.Key)
							   .ToArray();

		if (pendingEntries.Length > 0)
		{
			_sortedKeys = ArrayPool<int>.Shared.Rent(pendingEntries.Length);
			Array.Copy(pendingEntries, _sortedKeys, pendingEntries.Length);

			// Clear remaining slots from the rented array
			for (int i = pendingEntries.Length; i < _sortedKeys.Length; i++)
				_sortedKeys[i] = 0;
		}

		_lastSnapshotCount = currentCount;
	}

	private void ReturnSortedKeys()
	{
		if (_sortedKeys is { })
		{
			ArrayPool<int>.Shared.Return(_sortedKeys);
			_sortedKeys = null;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void ThrowIfDisposed()
	{
		if (Volatile.Read(ref _disposed) != 0)
			throw new ObjectDisposedException(nameof(ActivationService));
	}
}
