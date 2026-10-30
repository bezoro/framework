using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Bezoro.Core.Abstractions;
using Bezoro.Core.Extensions;
using Bezoro.Core.Internal;

namespace Bezoro.Core.Types.Pool;

/// <summary>
///     A high-performance, thread-safe object pool supporting configurable policies,
///     async waiting, and automatic capacity management.
/// </summary>
/// <typeparam name="T">The type of objects to pool. Must be a reference type.</typeparam>
[DebuggerDisplay("Available={AvailableCount}, Total={TotalCount}, Max={MaxCapacity}")]
public sealed class ObjectPool<T> : IPool<T>, IDisposable where T : class
{
	private readonly ConcurrentStack<T>                          _available;
	private readonly ConcurrentDictionary<T, PoolItemState> _ownedItems;
	private readonly object                                  _lifecycleGate = new();
	private readonly IPoolPolicy<T>                          _policy;
	private readonly PoolOptions                             _options;
	private readonly SemaphoreSlim?                          _asyncWaitSemaphore;
	private readonly CancellationTokenSource?                _disposeWaitCancellation;
	private int _activeWaiters;
	private bool _waitShutdownComplete;
	private bool _waitResourcesDisposed;

	private int  _disposed;
	private int  _totalCount;
	private long _totalAsyncWaits;
	private long _totalCreated;
	private long _totalDiscarded;
	private long _totalRented;
	private long _totalReturned;
	private long _totalTimeouts;

	/// <summary>
	///     Initializes a new pool with the specified factory and default options.
	/// </summary>
	/// <param name="factory">Factory function to create new instances.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="factory" /> is <c>null</c>.</exception>
	public ObjectPool(Func<T> factory)
		: this(new PoolPolicy<T>(factory), PoolOptions.Default) { }

	/// <summary>
	///     Initializes a new pool with the specified factory and options.
	/// </summary>
	/// <param name="factory">Factory function to create new instances.</param>
	/// <param name="options">Pool configuration options.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="factory" /> is <c>null</c>.</exception>
	public ObjectPool(Func<T> factory, PoolOptions options)
		: this(new PoolPolicy<T>(factory), options) { }

	/// <summary>
	///     Initializes a new pool with full policy control.
	/// </summary>
	/// <param name="policy">The lifecycle policy for pooled objects.</param>
	/// <param name="options">Pool configuration options.</param>
	/// <exception cref="ArgumentNullException">Thrown when <paramref name="policy" /> is <c>null</c>.</exception>
	public ObjectPool(IPoolPolicy<T> policy, PoolOptions options = default)
	{
		_policy     = policy.ThrowIfNull();
		_options    = options == default ? PoolOptions.Default : options;
		_available  = new();
		_ownedItems = new(ReferenceIdentityComparer<T>.Instance);

		if (_options.EnableAsyncWait && _options.MaxCapacity > 0)
		{
			_asyncWaitSemaphore = new(0, _options.MaxCapacity);
			_disposeWaitCancellation = new();
		}

		try
		{
			PrewarmPool();
		}
		catch
		{
			try { Dispose(); }
			catch { /* Preserve the creation failure after releasing prewarmed objects. */ }
			throw;
		}
	}

	/// <inheritdoc />
	public int AvailableCount => _available.Count;

	/// <inheritdoc />
	public int MaxCapacity => _options.MaxCapacity;

	/// <inheritdoc />
	public int TotalCount => Volatile.Read(ref _totalCount);

	/// <inheritdoc />
	public PoolStatistics Statistics => BuildStatistics();

	/// <inheritdoc />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool Return(T item)
	{
		item.ThrowIfNull();

		// Claim the object before callbacks can reset, publish, or discard it.
		// A foreign return owns an uncounted Returning marker until capacity is reserved.
		while (true)
		{
			if (_ownedItems.TryGetValue(item, out var state))
			{
				if (state != PoolItemState.Rented)
					return false;
				if (_ownedItems.TryUpdate(item, PoolItemState.Returning, PoolItemState.Rented))
					break;
				continue;
			}

			// Unknown objects cannot transfer ownership to a disposed pool. In particular,
			// a duplicate late return must not dispose a previously released object again.
			if (Volatile.Read(ref _disposed) != 0)
				return false;
			if (!_ownedItems.TryAdd(item, PoolItemState.Returning))
				continue;

			if (TryReserveCapacity())
				break;

			try { DiscardUnownedItem(item); }
			finally { TryRemoveOwnedItem(item, PoolItemState.Returning); }
			return false;
		}

		if (Volatile.Read(ref _disposed) != 0)
		{
			ReleaseOwnedItem(item, _policy.OnDiscard);
			return false;
		}

		bool canReturn;
		try { canReturn = ResetAndValidateForReturn(item); }
		catch
		{
			try { ReleaseOwnedItem(item, _policy.OnDiscard); }
			catch { /* Preserve the reset failure. */ }
			throw;
		}

		if (!canReturn || Volatile.Read(ref _disposed) != 0)
		{
			ReleaseOwnedItem(item, _policy.OnDiscard);
			return false;
		}

		return _ownedItems.TryUpdate(item, PoolItemState.Available, PoolItemState.Returning) &&
			   PublishReturnedItem(item);
	}

	/// <inheritdoc />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool TryRent([NotNullWhen(true)] out T? item) => TryAcquire(false, out item);

	/// <inheritdoc />
	public int TrimExcess(Percent targetUtilization = default)
	{
		if (targetUtilization == default)
			targetUtilization = Percent.Ninety;

		var trimmed   = 0;
		int available = _available.Count;
		int total     = TotalCount;

		if (total == 0) return 0;

		var targetAvailable = (int)(total * (100 - targetUtilization.Value) / 100.0);
		int toRemove        = available - targetAvailable;

		while (toRemove > 0 && _available.TryPop(out var item))
		{
			if (!_ownedItems.TryGetValue(item, out var state) || state != PoolItemState.Available)
				continue;

			ReleaseOwnedItem(item, _policy.OnDiscard);
			trimmed++;
			toRemove--;
		}

		return trimmed;
	}

	/// <inheritdoc />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public PooledObjectHandle<T> RentHandle()
	{
		var item = Rent();
		return new(item, this);
	}

	/// <inheritdoc />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public T Rent()
	{
		ThrowIfDisposed();
		return RentCore();
	}

	/// <inheritdoc />
	public async ValueTask<T?> RentAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
	{
		ThrowIfDisposed();
		if (timeout < TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)
			throw new ArgumentOutOfRangeException(nameof(timeout));

		var stopwatch = Stopwatch.StartNew();

		while (true)
		{
			ThrowIfDisposed();
			cancellationToken.ThrowIfCancellationRequested();
			if (TryAcquire(true, out var item))
				return item;

			if (_asyncWaitSemaphore is null || !_options.EnableAsyncWait)
				return null;

			var remaining = timeout == Timeout.InfiniteTimeSpan ? Timeout.InfiniteTimeSpan : timeout - stopwatch.Elapsed;
			if (timeout != Timeout.InfiniteTimeSpan && remaining <= TimeSpan.Zero)
			{
				if (_options.TrackStatistics)
					Interlocked.Increment(ref _totalTimeouts);

				return null;
			}

			if (_options.TrackStatistics)
				Interlocked.Increment(ref _totalAsyncWaits);

			bool acquired = await WaitForAvailableAsync(remaining, cancellationToken).ConfigureAwait(false);
			if (!acquired)
			{
				if (_options.TrackStatistics)
					Interlocked.Increment(ref _totalTimeouts);

				return null;
			}

			// Loop to try again - another thread may have grabbed the item
		}
	}

	/// <inheritdoc />
	public async ValueTask<T> RentAsync(CancellationToken cancellationToken = default)
	{
		ThrowIfDisposed();

		while (true)
		{
			ThrowIfDisposed();
			cancellationToken.ThrowIfCancellationRequested();
			if (TryAcquire(true, out var item))
				return item;

			if (_asyncWaitSemaphore is null || !_options.EnableAsyncWait)
				throw new PoolExhaustedException(typeof(T), _options.MaxCapacity);

			if (_options.TrackStatistics)
				Interlocked.Increment(ref _totalAsyncWaits);

			// Wait for an item to be returned, then loop to try again
			await WaitForAvailableAsync(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
		}
	}

	/// <summary>
	///     Clears all available objects from the pool and directly disposes items that implement
	///     <see cref="IDisposable" /> without invoking <see cref="IPoolPolicy{T}.OnDiscard" />.
	/// </summary>
	public void Clear() => ClearAvailable(DisposeItem);

	/// <summary>
	///     Clears all available objects from the pool by invoking <see cref="IPoolPolicy{T}.OnDiscard" />
	///     for each item.
	/// </summary>
	public void ClearWithPolicyDiscard() => ClearAvailable(_policy.OnDiscard);

	/// <summary>
	///     Clears all available objects from the pool using the selected legacy release behavior.
	/// </summary>
	/// <param name="disposeItems">
	///     If <c>true</c>, forwards to <see cref="Clear()" />; otherwise, forwards to
	///     <see cref="ClearWithPolicyDiscard" />.
	/// </param>
	[Obsolete("Use Clear() or ClearWithPolicyDiscard() instead.")]
	public void Clear(bool disposeItems)
	{
		if (disposeItems)
			Clear();
		else
			ClearWithPolicyDiscard();
	}

	/// <summary>
	///     Disposes the pool and all managed resources.
	/// </summary>
	public void Dispose()
	{
		lock (_lifecycleGate)
		{
			if (Volatile.Read(ref _disposed) != 0)
				return;

			Volatile.Write(ref _disposed, 1);
		}

		try
		{
			Clear();
		}
		finally
		{
			try
			{
				_disposeWaitCancellation?.Cancel();
			}
			finally
			{
				lock (_lifecycleGate)
				{
					_waitShutdownComplete = true;
					DisposeWaitResourcesIfReady();
				}
			}
		}
	}

	private async Task<bool> WaitForAvailableAsync(TimeSpan timeout, CancellationToken cancellationToken)
	{
		CancellationTokenSource linkedCancellation;
		lock (_lifecycleGate)
		{
			ThrowIfDisposed();
			linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
				cancellationToken, _disposeWaitCancellation!.Token);
			_activeWaiters++;
		}

		try
		{
			return await _asyncWaitSemaphore!.WaitAsync(timeout, linkedCancellation.Token).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (Volatile.Read(ref _disposed) != 0)
		{
			throw new ObjectDisposedException(nameof(ObjectPool<T>));
		}
		finally
		{
			linkedCancellation.Dispose();
			lock (_lifecycleGate)
			{
				_activeWaiters--;
				DisposeWaitResourcesIfReady();
			}
		}
	}

	private void DisposeWaitResourcesIfReady()
	{
		if (!_waitShutdownComplete || _activeWaiters != 0 || _waitResourcesDisposed)
			return;

		_waitResourcesDisposed = true;
		_asyncWaitSemaphore?.Dispose();
		_disposeWaitCancellation?.Dispose();
	}

	private static void DisposeItem(T item)
	{
		if (item is IDisposable disposable)
			disposable.Dispose();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void NotifyRent(T item)
	{
		try
		{
			if (item is IPooledObject pooled)
				pooled.OnRent();
		}
		catch
		{
			try
			{
				try
				{
					ReleaseOwnedItem(item, _policy.OnDiscard);
				}
				finally
				{
					lock (_lifecycleGate)
					{
						if (Volatile.Read(ref _disposed) == 0)
							TrySignalWaiters();
					}
				}
			}
			catch
			{
				// Cleanup or notification failure must not replace the OnRent exception.
			}

			throw;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool ResetAndValidateForReturn(T item) => _policy.Reset(item);

	private void ClearAvailable(Action<T> release)
	{
		ExceptionDispatchInfo? failure = null;
		while (_available.TryPop(out var item))
		{
			try { ReleaseOwnedItem(item, release); }
			catch (Exception ex) { failure ??= ExceptionDispatchInfo.Capture(ex); }
		}

		failure?.Throw();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void DiscardUnownedItem(T item)
	{
		try
		{
			_policy.OnDiscard(item);
		}
		finally
		{
			if (_options.TrackStatistics)
				Interlocked.Increment(ref _totalDiscarded);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void IncrementRented()
	{
		if (_options.TrackStatistics)
			Interlocked.Increment(ref _totalRented);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void IncrementReturned()
	{
		if (_options.TrackStatistics)
			Interlocked.Increment(ref _totalReturned);
	}

	private void PrewarmPool()
	{
		for (var i = 0; i < _options.InitialCapacity; i++)
		{
			if (!TryCreateOwnedItem(out var item))
				break;

			if (!_ownedItems.TryUpdate(item, PoolItemState.Available, PoolItemState.Rented))
			{
				ReleaseOwnedItem(item, _policy.OnDiscard);
				throw new InvalidOperationException("Failed to register a prewarmed item as available.");
			}

			_available.Push(item);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool PublishReturnedItem(T item)
	{
		lock (_lifecycleGate)
		{
			if (Volatile.Read(ref _disposed) == 0)
			{
				_available.Push(item);
				IncrementReturned();
				TrySignalWaiters();
				return true;
			}
		}

		ReleaseOwnedItem(item, _policy.OnDiscard);
		return false;
	}

	private bool ReleaseOwnedItem(T item, Action<T> release)
	{
		while (true)
		{
			if (!_ownedItems.TryGetValue(item, out var state) || state == PoolItemState.Discarding)
				return false;
			if (_ownedItems.TryUpdate(item, PoolItemState.Discarding, state))
				break;
		}

		try
		{
			release(item);
		}
		finally
		{
			// Keep the marker while user disposal code runs so reentrant and concurrent
			// returns cannot reset or dispose the same object during that callback.
			TryRemoveOwnedItem(item, PoolItemState.Discarding);
			Interlocked.Decrement(ref _totalCount);
			if (_options.TrackStatistics) Interlocked.Increment(ref _totalDiscarded);
			lock (_lifecycleGate)
			{
				if (Volatile.Read(ref _disposed) == 0)
					TrySignalWaiters();
			}
		}

		return true;
	}

	private bool TryAcquire(bool allowCreate, [NotNullWhen(true)] out T? item)
	{
		if (Volatile.Read(ref _disposed) != 0)
		{
			item = null;
			return false;
		}

		while (_available.TryPop(out var availableItem))
		{
			if (!_ownedItems.TryUpdate(availableItem, PoolItemState.Rented, PoolItemState.Available))
				continue;

			bool valid;
			try
			{
				valid = !_options.ValidateOnRent || _policy.Validate(availableItem);
			}
			catch
			{
				try { ReleaseOwnedItem(availableItem, _policy.OnDiscard); }
				catch { /* Preserve the validation failure. */ }
				throw;
			}

			if (!valid)
			{
				ReleaseOwnedItem(availableItem, _policy.OnDiscard);
				continue;
			}

			item = availableItem;
			NotifyRent(item);
			IncrementRented();
			return true;
		}

		if (!allowCreate)
		{
			item = null;
			return false;
		}

		if (!TryCreateOwnedItem(out item))
			return false;

		NotifyRent(item);
		IncrementRented();
		return true;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool TryCreateOwnedItem([NotNullWhen(true)] out T? item)
	{
		item = null;
		if (!TryReserveCapacity())
			return false;

		try
		{
			var created = _policy.Create();
			if (created is null)
				throw new InvalidOperationException("Pool policy returned null from Create().");

			if (!_ownedItems.TryAdd(created, PoolItemState.Rented))
				throw new InvalidOperationException("Pool policy returned an instance already owned by the pool.");

			item = created;

			if (_options.TrackStatistics)
				Interlocked.Increment(ref _totalCreated);

			return true;
		}
		catch
		{
			Interlocked.Decrement(ref _totalCount);
			throw;
		}
	}


	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool TryRemoveOwnedItem(T item, PoolItemState state) =>
		((ICollection<KeyValuePair<T, PoolItemState>>)_ownedItems).Remove(new(item, state));

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool TryReserveCapacity()
	{
		while (true)
		{
			int currentCount = Volatile.Read(ref _totalCount);

			if (_options.MaxCapacity > 0 && currentCount >= _options.MaxCapacity)
				return false;

			if (Interlocked.CompareExchange(ref _totalCount, currentCount + 1, currentCount) == currentCount)
				return true;
		}
	}

	private PoolStatistics BuildStatistics()
	{
		int  total       = TotalCount;
		int  available   = AvailableCount;
		int  rented      = total - available;
		byte utilization = total > 0 ? (byte)(rented * 100 / total) : (byte)0;

		return new()
		{
			TotalRented     = Volatile.Read(ref _totalRented),
			TotalReturned   = Volatile.Read(ref _totalReturned),
			TotalCreated    = Volatile.Read(ref _totalCreated),
			TotalDiscarded  = Volatile.Read(ref _totalDiscarded),
			TotalAsyncWaits = Volatile.Read(ref _totalAsyncWaits),
			TotalTimeouts   = Volatile.Read(ref _totalTimeouts),
			Utilization     = new(utilization > 100 ? (byte)100 : utilization)
		};
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private T RentCore()
	{
		if (TryAcquire(true, out var item)) return item;
		throw new PoolExhaustedException(typeof(T), _options.MaxCapacity);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void ThrowIfDisposed()
	{
		if (Volatile.Read(ref _disposed) != 0)
			throw new ObjectDisposedException(nameof(ObjectPool<T>));
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void TrySignalWaiters()
	{
		if (_asyncWaitSemaphore is null)
			return;

		try
		{
			_asyncWaitSemaphore.Release();
		}
		catch (SemaphoreFullException)
		{
			// No waiters - semaphore already at max, safe to ignore
		}
	}
}
