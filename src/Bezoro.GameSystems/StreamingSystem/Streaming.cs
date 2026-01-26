using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Bezoro.GameSystems.StreamingSystem;

/// <summary>
///     A thread-safe streaming system that evaluates entity distances in a background thread
///     and invokes streaming callbacks on the main thread.
/// </summary>
public sealed class Streaming : IDisposable
{
	private readonly ConcurrentDictionary<int, IStreamableEntity> _entities     = new();
	private readonly ConcurrentQueue<StreamingResult>             _resultsQueue = new();
	private          CancellationTokenSource?                     _cts;
	private          float                                        _inDistanceSquared;
	private          float                                        _outDistanceSquared;
	private          int                                          _cachedKeyCount;
	private          int                                          _currentIndex;
	private          int                                          _disposed;
	private          int                                          _lastKnownEntityCount;

	// Cached entity snapshot for iteration (avoids per-frame allocation)
	private int[]? _cachedKeys;

	private StreamingConfig         _config;
	private SynchronizationContext? _syncContext;
	private Task?                   _processingTask;

	/// <summary>
	///     Gets whether the streaming system is currently running.
	/// </summary>
	public bool IsRunning => _processingTask is { IsCompleted: false };

	/// <summary>
	///     Gets the current number of registered entities.
	/// </summary>
	public int EntityCount => _entities.Count;

	/// <summary>
	///     Disposes the streaming system and releases all resources.
	/// </summary>
	public void Dispose()
	{
		if (Interlocked.Exchange(ref _disposed, 1) != 0)
			return;

		Stop();
		_entities.Clear();
		ReturnCachedKeys();
	}

	/// <summary>
	///     Registers an entity with the streaming system.
	/// </summary>
	/// <param name="entity">The entity to register.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="entity" /> is null.</exception>
	/// <exception cref="ObjectDisposedException">Thrown when the system has been disposed.</exception>
	public void Register(IStreamableEntity entity)
	{
		ThrowIfDisposed();

		if (entity is null)
			throw new ArgumentNullException(nameof(entity));

		_entities.TryAdd(entity.EntityId, entity);
	}

	/// <summary>
	///     Starts the streaming system with the specified configuration.
	/// </summary>
	/// <param name="config">The streaming configuration.</param>
	/// <exception cref="ArgumentNullException">
	///     Thrown when <paramref name="config" />.GetReferencePosition is null.
	/// </exception>
	/// <exception cref="ArgumentException">
	///     Thrown when StreamOutDistance is less than StreamInDistance.
	/// </exception>
	/// <exception cref="ObjectDisposedException">Thrown when the system has been disposed.</exception>
	public void Start(StreamingConfig config)
	{
		ThrowIfDisposed();

		if (IsRunning) return;

		if (config.GetReferencePosition is null)
			throw new ArgumentNullException(nameof(config), "GetReferencePosition delegate cannot be null.");

		if (config.StreamOutDistance < config.StreamInDistance)
			throw new ArgumentException(
				$"StreamOutDistance ({config.StreamOutDistance}) must be >= StreamInDistance ({config.StreamInDistance}) to prevent flickering.",
				nameof(config));

		_config             = config;
		_inDistanceSquared  = config.StreamInDistance * config.StreamInDistance;
		_outDistanceSquared = config.StreamOutDistance * config.StreamOutDistance;
		_syncContext        = config.CallbackContext;
		_currentIndex       = 0;

		_cts = new();
		var token = _cts.Token;

		_processingTask = Task.Run(() => ProcessingLoopAsync(token), token);
	}

	/// <summary>
	///     Stops the streaming system.
	/// </summary>
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

		// Clear any pending results
		while (_resultsQueue.TryDequeue(out _)) { }

		// Return cached keys to pool
		ReturnCachedKeys();
		_lastKnownEntityCount = 0;
	}

	/// <summary>
	///     Unregisters an entity from the streaming system.
	/// </summary>
	/// <param name="entity">The entity to unregister.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="entity" /> is null.</exception>
	public void Unregister(IStreamableEntity entity)
	{
		if (entity is null)
			throw new ArgumentNullException(nameof(entity));

		_entities.TryRemove(entity.EntityId, out _);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private int RebuildCacheIfNeeded(int entityCount)
	{
		// Only rebuild when collection has changed
		if (_cachedKeys is { } && _lastKnownEntityCount == entityCount)
			return _cachedKeyCount;

		// Return old buffer if exists
		ReturnCachedKeys();

		// Rent new buffer from pool (may be larger than needed)
		int[]? keys  = ArrayPool<int>.Shared.Rent(entityCount);
		var    index = 0;
		foreach (var kvp in _entities)
		{
			if (index >= entityCount)
				break;

			keys[index++] = kvp.Key;
		}

		_cachedKeys           = keys;
		_cachedKeyCount       = index;
		_lastKnownEntityCount = entityCount;
		return index;
	}

	private async Task ProcessingLoopAsync(CancellationToken ct)
	{
		while (!ct.IsCancellationRequested)
		{
			try
			{
				ProcessEntities(ct);
				FlushResults();
				await Task.Delay(_config.FrameDelayMs, ct).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				// Expected during shutdown
				break;
			}
			catch (Exception)
			{
				// Log and continue - don't let exceptions kill the loop
				// In production, you'd want proper logging here
			}
		}
	}

	private void ApplyResultsDirect()
	{
		while (_resultsQueue.TryDequeue(out var result))
		{
			// Check entity still exists before invoking callback
			if (!_entities.TryGetValue(result.EntityId, out var entity))
				continue;

			try
			{
				if (result.ShouldStreamIn)
					entity.OnStreamIn();
				else
					entity.OnStreamOut();
			}
			catch
			{
				// Don't let callback exceptions crash the system
				// In production, you'd want proper logging here
			}
		}
	}

	private void FlushResults()
	{
		if (_resultsQueue.IsEmpty)
			return;

		if (_syncContext is null)
		{
			// No sync context - apply directly (warning should be logged in production)
			ApplyResultsDirect();
			return;
		}

		_syncContext.Post(_ => ApplyResultsDirect(), null);
	}

	private void ProcessEntities(CancellationToken ct)
	{
		int entityCount = _entities.Count;
		if (entityCount == 0)
			return;

		// Get the reference position once per iteration
		Vector3 referencePos;
		try
		{
			referencePos = _config.GetReferencePosition();
		}
		catch
		{
			// If getting reference position fails, skip this iteration
			return;
		}

		// Rebuild cached keys only when entity count changes (dirty tracking)
		int actualCount = RebuildCacheIfNeeded(entityCount);
		if (actualCount == 0)
			return;

		int[] keys = _cachedKeys!;

		// Ensure the current index is within bounds
		if (_currentIndex >= actualCount)
			_currentIndex = 0;

		var processed   = 0;
		int maxPerFrame = _config.MaxPerFrame > 0 ? _config.MaxPerFrame : actualCount;
		int endIndex    = Math.Min(maxPerFrame, actualCount);

		// Cache squared distances locally for faster access
		float inDistSq  = _inDistanceSquared;
		float outDistSq = _outDistanceSquared;

		while (processed < endIndex && !ct.IsCancellationRequested)
		{
			int entityId = keys[_currentIndex];

			if (_entities.TryGetValue(entityId, out var entity))
			{
				// Inline distance squared calculation (SIMD-optimized in System.Numerics)
				var   diff   = referencePos - entity.StreamingPosition;
				float distSq = Vector3.Dot(diff, diff);
				bool  isIn   = entity.IsStreamedIn;

				// Apply hysteresis: stream in if close enough and not already in
				// stream out if far enough and currently streamed in
				if (!isIn && distSq <= inDistSq)
					_resultsQueue.Enqueue(new(entityId, true));
				else if (isIn && distSq > outDistSq)
					_resultsQueue.Enqueue(new(entityId, false));
			}

			// Avoid modulo: use branch instead (branch prediction friendly)
			_currentIndex++;
			if (_currentIndex >= actualCount)
				_currentIndex = 0;

			processed++;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void ReturnCachedKeys()
	{
		int[]? keys = Interlocked.Exchange(ref _cachedKeys, null);
		if (keys is { })
			ArrayPool<int>.Shared.Return(keys);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void ThrowIfDisposed()
	{
		if (Volatile.Read(ref _disposed) != 0)
			throw new ObjectDisposedException(nameof(Streaming));
	}

	/// <summary>
	///     Internal struct to hold streaming operation results.
	/// </summary>
	private readonly struct StreamingResult(int entityId, bool shouldStreamIn)
	{
		public readonly bool ShouldStreamIn = shouldStreamIn;
		public readonly int  EntityId       = entityId;
	}
}
