using System;
using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Bezoro.GameSystems.Streaming;

/// <summary>
///     A thread-safe streaming system that evaluates entity distances in a background thread
///     and invokes streaming callbacks on the main thread.
/// </summary>
public sealed class StreamingSystem : IDisposable
{
	private static readonly Lazy<StreamingSystem> LazyInstance = new(
		() => new StreamingSystem(),
		LazyThreadSafetyMode.ExecutionAndPublication);

	private readonly ConcurrentDictionary<int, IStreamableEntity> _entities = new();
	private readonly ConcurrentQueue<StreamingResult> _resultsQueue = new();

	private StreamingConfig _config;
	private CancellationTokenSource? _cts;
	private int _currentIndex;
	private int _disposed;
	private float _inDistanceSquared;
	private float _outDistanceSquared;
	private Task? _processingTask;
	private SynchronizationContext? _syncContext;

	private StreamingSystem() { }

	/// <summary>
	///     Gets the singleton instance of the streaming system.
	/// </summary>
	public static StreamingSystem Instance => LazyInstance.Value;

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
	}

	/// <summary>
	///     Starts the streaming system with the specified configuration.
	/// </summary>
	/// <param name="config">The streaming configuration.</param>
	/// <exception cref="ArgumentNullException">
	///     Thrown when <paramref name="config" />.GetReferencePosition is null.
	/// </exception>
	/// <exception cref="ObjectDisposedException">Thrown when the system has been disposed.</exception>
	public void Start(StreamingConfig config)
	{
		ThrowIfDisposed();

		if (IsRunning)
			return;

		if (config.GetReferencePosition is null)
			throw new ArgumentNullException(nameof(config), "GetReferencePosition delegate cannot be null.");

		_config = config;
		_inDistanceSquared = config.StreamInDistance * config.StreamInDistance;
		_outDistanceSquared = config.StreamOutDistance * config.StreamOutDistance;
		_syncContext = SynchronizationContext.Current;
		_currentIndex = 0;

		_cts = new CancellationTokenSource();
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
		_cts = null;
		_processingTask = null;

		// Clear any pending results
		while (_resultsQueue.TryDequeue(out _)) { }
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

	private void ProcessEntities(CancellationToken ct)
	{
		var entityCount = _entities.Count;
		if (entityCount == 0)
			return;

		// Get reference position once per iteration
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

		// Get snapshot of entity IDs for round-robin processing
		var keys = new int[entityCount];
		var index = 0;
		foreach (var kvp in _entities)
		{
			if (index >= entityCount)
				break;
			keys[index++] = kvp.Key;
		}

		var actualCount = index;
		if (actualCount == 0)
			return;

		// Ensure current index is within bounds
		if (_currentIndex >= actualCount)
			_currentIndex = 0;

		var processed = 0;
		var maxPerFrame = _config.MaxPerFrame > 0 ? _config.MaxPerFrame : actualCount;

		while (processed < maxPerFrame && !ct.IsCancellationRequested)
		{
			var entityId = keys[_currentIndex];

			if (_entities.TryGetValue(entityId, out var entity))
			{
				var distSq = DistanceSquared(referencePos, entity.StreamingPosition);
				var isStreamedIn = entity.IsStreamedIn;

				// Apply hysteresis: stream in if close enough and not already in
				// stream out if far enough and currently streamed in
				if (!isStreamedIn && distSq <= _inDistanceSquared)
				{
					_resultsQueue.Enqueue(new StreamingResult(entityId, shouldStreamIn: true));
				}
				else if (isStreamedIn && distSq > _outDistanceSquared)
				{
					_resultsQueue.Enqueue(new StreamingResult(entityId, shouldStreamIn: false));
				}
			}

			_currentIndex = (_currentIndex + 1) % actualCount;
			processed++;

			// If we've processed all entities, break early
			if (processed >= actualCount)
				break;
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

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static float DistanceSquared(Vector3 a, Vector3 b)
	{
		var dx = a.X - b.X;
		var dy = a.Y - b.Y;
		var dz = a.Z - b.Z;
		return dx * dx + dy * dy + dz * dz;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void ThrowIfDisposed()
	{
		if (Volatile.Read(ref _disposed) != 0)
			throw new ObjectDisposedException(nameof(StreamingSystem));
	}

	/// <summary>
	///     Internal struct to hold streaming operation results.
	/// </summary>
	private readonly struct StreamingResult
	{
		public readonly int EntityId;
		public readonly bool ShouldStreamIn;

		public StreamingResult(int entityId, bool shouldStreamIn)
		{
			EntityId = entityId;
			ShouldStreamIn = shouldStreamIn;
		}
	}
}
