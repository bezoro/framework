using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
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
			_asyncWaitSemaphore = new(0, _options.MaxCapacity);

		PrewarmPool();
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

		if (Volatile.Read(ref _disposed) != 0)
		{
			ReleaseReturnedItem(item);
			return false;
		}

		bool canReturn = ResetAndValidateForReturn(item);

		if (!canReturn)
		{
			if (_ownedItems.TryGetValue(item, out var rejectedState) && rejectedState == PoolItemState.Available)
				return false;

			ReleaseReturnedItem(item);
			return false;
		}

		if (Volatile.Read(ref _disposed) != 0)
		{
			ReleaseReturnedItem(item);
			return false;
		}

		while (true)
		{
			if (_ownedItems.TryGetValue(item, out var state))
			{
				if (state == PoolItemState.Available)
					return false;

				if (_ownedItems.TryUpdate(item, PoolItemState.Available, PoolItemState.Rented))
					return PublishReturnedItem(item);

				continue;
			}

			if (TryRegisterForeignItem(item, out bool retryOwnership))
				return PublishReturnedItem(item);

			if (!retryOwnership)
				return false;
		}
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

		var stopwatch = Stopwatch.StartNew();

		while (true)
		{
			if (TryAcquire(true, out var item))
				return item;

			if (_asyncWaitSemaphore is null || !_options.EnableAsyncWait)
				return null;

			var remaining = timeout - stopwatch.Elapsed;
			if (remaining <= TimeSpan.Zero)
			{
				if (_options.TrackStatistics)
					Interlocked.Increment(ref _totalTimeouts);

				return null;
			}

			if (_options.TrackStatistics)
				Interlocked.Increment(ref _totalAsyncWaits);

			bool acquired = await _asyncWaitSemaphore.WaitAsync(remaining, cancellationToken).ConfigureAwait(false);
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
			if (TryAcquire(true, out var item))
				return item;

			if (_asyncWaitSemaphore is null || !_options.EnableAsyncWait)
				throw new PoolExhaustedException(typeof(T), _options.MaxCapacity);

			if (_options.TrackStatistics)
				Interlocked.Increment(ref _totalAsyncWaits);

			// Wait for an item to be returned, then loop to try again
			await _asyncWaitSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
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

		Clear();
		_asyncWaitSemaphore?.Dispose();
	}

	private static void DisposeItem(T item)
	{
		if (item is IDisposable disposable)
			disposable.Dispose();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void NotifyRent(T item)
	{
		if (item is IPooledObject pooled)
			pooled.OnRent();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool ResetAndValidateForReturn(T item) => _policy.Reset(item);

	private void ClearAvailable(Action<T> release)
	{
		while (_available.TryPop(out var item))
			ReleaseOwnedItem(item, release);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void DiscardUnownedItem(T item)
	{
		_policy.OnDiscard(item);

		if (_options.TrackStatistics)
			Interlocked.Increment(ref _totalDiscarded);
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
		if (!_ownedItems.TryRemove(item, out _)) return false;

		try
		{
			release(item);
		}
		finally
		{
			Interlocked.Decrement(ref _totalCount);
			if (_options.TrackStatistics) Interlocked.Increment(ref _totalDiscarded);
		}

		return true;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void ReleaseReturnedItem(T item)
	{
		if (!ReleaseOwnedItem(item, _policy.OnDiscard))
			DiscardUnownedItem(item);
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

			if (_options.ValidateOnRent && !_policy.Validate(availableItem))
			{
				ReleaseOwnedItem(availableItem, _policy.OnDiscard);
				continue;
			}

			item = availableItem;
			IncrementRented();
			NotifyRent(item);
			return true;
		}

		if (!allowCreate)
		{
			item = null;
			return false;
		}

		if (!TryCreateOwnedItem(out item))
			return false;

		IncrementRented();
		NotifyRent(item);
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

	private bool TryRegisterForeignItem(T item, out bool retryOwnership)
	{
		while (true)
		{
			retryOwnership = false;

			if (TryReserveCapacity())
			{
				if (_ownedItems.TryAdd(item, PoolItemState.Available))
					return true;

				Interlocked.Decrement(ref _totalCount);
				retryOwnership = true;
				return false;
			}

			if (!_ownedItems.TryAdd(item, PoolItemState.Available))
			{
				retryOwnership = true;
				return false;
			}

			if (TryReplaceRentedItem(item))
				return true;

			if (!TryRemoveOwnedItem(item, PoolItemState.Available))
			{
				retryOwnership = true;
				return false;
			}

			if (_options.MaxCapacity <= 0 || Volatile.Read(ref _totalCount) < _options.MaxCapacity)
				continue;

			DiscardUnownedItem(item);
			return false;
		}
	}

	private bool TryReplaceRentedItem(T replacement)
	{
		foreach (var pair in _ownedItems)
		{
			if (ReferenceEquals(pair.Key, replacement) || pair.Value != PoolItemState.Rented)
				continue;

			if (TryRemoveOwnedItem(pair.Key, PoolItemState.Rented))
				return true;
		}

		return false;
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
