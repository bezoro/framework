using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.GameSystems.TimerSystem.Abstractions;
using Bezoro.GameSystems.TimerSystem.Types;

namespace Bezoro.GameSystems.TimerSystem.Services;

/// <summary>
///     A thread-safe timer service that ticks all timers in a background loop
///     and invokes callbacks when timers complete.
/// </summary>
public sealed class TimerService : ITimerService
{
	private readonly ConcurrentDictionary<int, TimerEntry>   _timers        = new();
	private readonly ConcurrentQueue<CompletedTimerCallback> _callbackQueue = new();
	private          CancellationTokenSource?                _cts;
	private          int                                     _disposed;
	private          int                                     _nextId;
	private          SynchronizationContext?                 _syncContext;
	private          Task?                                   _processingTask;
	private          TimerConfig                             _config;

	/// <inheritdoc />
	public event Action<TimerCompletedEventArgs>? TimerCompleted;

	/// <inheritdoc />
	public TimerHandle Create(TimeSpan duration, Action<TimerHandle>? onCompleted = null)
	{
		ThrowIfDisposed();

		if (duration <= TimeSpan.Zero)
			throw new ArgumentOutOfRangeException(nameof(duration), "Duration must be positive.");

		int id     = Interlocked.Increment(ref _nextId);
		var handle = new TimerHandle(id);

		var durationTicks = (long)(duration.TotalSeconds * Stopwatch.Frequency);
		var entry         = new TimerEntry(durationTicks, onCompleted);

		_timers.TryAdd(id, entry);

		return handle;
	}

	/// <inheritdoc />
	public bool IsRunning => _processingTask is { IsCompleted: false };

	/// <inheritdoc />
	public int ActiveCount => _timers.Count(kvp => kvp.Value.State is TimerState.Running or TimerState.Paused);

	/// <inheritdoc />
	public bool Cancel(TimerHandle handle)
	{
		if (!handle.IsValid) return false;

		var transitioned = false;

		_timers.AddOrUpdate(
			handle.Id,
			_ => default,
			(_, existing) =>
			{
				if (existing.State is TimerState.Completed or TimerState.Stopped)
					return existing;

				existing.State = TimerState.Stopped;
				transitioned   = true;
				return existing;
			}
		);

		return transitioned;
	}

	/// <inheritdoc />
	public bool Pause(TimerHandle handle)
	{
		if (!handle.IsValid) return false;

		var transitioned = false;

		_timers.AddOrUpdate(
			handle.Id,
			_ => default, // Key not found — factory won't be used meaningfully
			(_, existing) =>
			{
				if (existing.State != TimerState.Running)
					return existing;

				long now = Stopwatch.GetTimestamp();
				existing.AccumulatedTicks += now - existing.StartTimestamp;
				existing.State            =  TimerState.Paused;
				transitioned              =  true;
				return existing;
			}
		);

		return transitioned;
	}

	/// <inheritdoc />
	public bool Restart(TimerHandle handle)
	{
		if (!handle.IsValid) return false;

		var restarted = false;

		_timers.AddOrUpdate(
			handle.Id,
			_ => default,
			(_, existing) =>
			{
				existing.AccumulatedTicks = 0;
				existing.StartTimestamp   = Stopwatch.GetTimestamp();
				existing.State            = TimerState.Running;
				restarted                 = true;
				return existing;
			}
		);

		return restarted;
	}

	/// <inheritdoc />
	public bool Resume(TimerHandle handle)
	{
		if (!handle.IsValid) return false;

		var transitioned = false;

		_timers.AddOrUpdate(
			handle.Id,
			_ => default,
			(_, existing) =>
			{
				if (existing.State != TimerState.Paused)
					return existing;

				existing.StartTimestamp = Stopwatch.GetTimestamp();
				existing.State          = TimerState.Running;
				transitioned            = true;
				return existing;
			}
		);

		return transitioned;
	}

	/// <inheritdoc />
	public bool TryGetInfo(TimerHandle handle, out TimerInfo info)
	{
		if (!handle.IsValid || !_timers.TryGetValue(handle.Id, out var entry))
		{
			info = default;
			return false;
		}

		long now          = Stopwatch.GetTimestamp();
		long elapsedTicks = entry.GetElapsedTicks(now);

		info = new(handle, entry.State, entry.DurationTicks, elapsedTicks);
		return true;
	}

	/// <inheritdoc />
	public int Cleanup()
	{
		var removed = 0;

		foreach (var kvp in _timers)
		{
			if (kvp.Value.State is TimerState.Completed or TimerState.Stopped)
				if (_timers.TryRemove(kvp.Key, out _))
					removed++;
		}

		return removed;
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (Interlocked.Exchange(ref _disposed, 1) != 0)
			return;

		Stop();
		_timers.Clear();
	}

	/// <inheritdoc />
	public void Start(TimerConfig config)
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
	}

	private async Task ProcessingLoopAsync(CancellationToken ct)
	{
		while (!ct.IsCancellationRequested)
		{
			try
			{
				Tick();
				FlushCallbacks();
				await Task.Delay(_config.TickRateMs, ct).ConfigureAwait(false);
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
		while (_callbackQueue.TryDequeue(out var cb))
		{
			try
			{
				cb.OnCompleted?.Invoke(cb.Handle);
			}
			catch
			{
				// Don't let callback exceptions crash the system
			}

			try
			{
				TimerCompleted?.Invoke(new(cb.Handle));
			}
			catch
			{
				// Don't let event handler exceptions crash the system
			}
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void ThrowIfDisposed()
	{
		if (Volatile.Read(ref _disposed) != 0)
			throw new ObjectDisposedException(nameof(TimerService));
	}

	private void Tick()
	{
		long now = Stopwatch.GetTimestamp();

		foreach (var kvp in _timers)
		{
			if (kvp.Value.State != TimerState.Running)
				continue;

			if (!kvp.Value.IsExpired(now))
				continue;

			// Transition to Completed atomically
			_timers.AddOrUpdate(
				kvp.Key,
				_ => default,
				(_, existing) =>
				{
					if (existing.State != TimerState.Running)
						return existing;

					if (!existing.IsExpired(now))
						return existing;

					existing.AccumulatedTicks = existing.DurationTicks;
					existing.State            = TimerState.Completed;

					var handle = new TimerHandle(kvp.Key);
					_callbackQueue.Enqueue(new(handle, existing.OnCompleted));

					return existing;
				}
			);
		}
	}

	private readonly struct CompletedTimerCallback(TimerHandle handle, Action<TimerHandle>? onCompleted)
	{
		public readonly Action<TimerHandle>? OnCompleted = onCompleted;
		public readonly TimerHandle          Handle      = handle;
	}
}
