using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Bezoro.Core.Abstractions;
using Bezoro.Core.Extensions;

namespace Bezoro.Core.Types.Pool;

/// <summary>
///     A high-performance, thread-safe object pool supporting configurable policies,
///     async waiting, and automatic capacity management.
/// </summary>
/// <typeparam name="T">The type of objects to pool. Must be a reference type.</typeparam>
[DebuggerDisplay("Available={AvailableCount}, Total={TotalCount}, Max={MaxCapacity}")]
public sealed class ObjectPool<T> : IPool<T>, IDisposable where T : class
{
	private readonly ConcurrentStack<T> _available;
	private readonly IPoolPolicy<T>     _policy;
	private readonly PoolOptions        _options;
	private readonly SemaphoreSlim?     _asyncWaitSemaphore;

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
		_policy    = policy.ThrowIfNull();
		_options   = options == default ? PoolOptions.Default : options;
		_available = new();

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
			DiscardItem(item);
			return false;
		}

		if (!ResetAndValidateForReturn(item))
		{
			DiscardItem(item);
			return false;
		}

		if (ShouldDiscard())
		{
			DiscardItem(item);
			return false;
		}

		_available.Push(item);
		IncrementReturned();
		TrySignalWaiters();
		return true;
	}

	/// <inheritdoc />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool TryRent([NotNullWhen(true)] out T? item)
	{
		if (Volatile.Read(ref _disposed) != 0)
		{
			item = null;
			return false;
		}

		while (_available.TryPop(out item))
		{
			if (!_options.ValidateOnRent || _policy.Validate(item))
			{
				IncrementRented();
				NotifyRent(item);
				return true;
			}

			DiscardItem(item);
		}

		item = null;
		return false;
	}

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
			DiscardItem(item);
			Interlocked.Decrement(ref _totalCount);
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
			if (TryRent(out var item))
				return item;

			// Atomically try to create a new item (if below max capacity)
			if (TryCreateNewItem(out var newItem))
				return newItem;

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
			if (TryRent(out var item))
				return item;

			// Atomically try to create a new item (if below max capacity)
			if (TryCreateNewItem(out var newItem))
				return newItem;

			if (_asyncWaitSemaphore is null || !_options.EnableAsyncWait)
				throw new PoolExhaustedException(typeof(T), _options.MaxCapacity);

			if (_options.TrackStatistics)
				Interlocked.Increment(ref _totalAsyncWaits);

			// Wait for an item to be returned, then loop to try again
			await _asyncWaitSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		}
	}

	/// <inheritdoc />
	public void Clear(bool disposeItems = true)
	{
		while (_available.TryPop(out var item))
		{
			if (disposeItems)
				DisposeItem(item);
			else
				_policy.OnDiscard(item);

			Interlocked.Decrement(ref _totalCount);
		}
	}

	/// <summary>
	///     Disposes the pool and all managed resources.
	/// </summary>
	public void Dispose()
	{
		if (Interlocked.Exchange(ref _disposed, 1) != 0)
			return;

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

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool ShouldDiscard()
	{
		if (_options.MaxCapacity < 0)
			return false;

		return _available.Count >= _options.MaxCapacity;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private bool TryCreateNewItem([NotNullWhen(true)] out T? item)
	{
		// Atomically reserve a slot before creating the item to prevent race conditions
		while (true)
		{
			int currentCount = Volatile.Read(ref _totalCount);

			if (_options.MaxCapacity > 0 && currentCount >= _options.MaxCapacity)
			{
				item = null;
				return false;
			}

			// Try to atomically increment the count to reserve a slot
			if (Interlocked.CompareExchange(ref _totalCount, currentCount + 1, currentCount) == currentCount)
				break;

			// Another thread modified the count, retry
		}

		item = _policy.Create();

		if (_options.TrackStatistics)
			Interlocked.Increment(ref _totalCreated);

		IncrementRented();
		NotifyRent(item);
		return true;
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
	private T CreateNewItem()
	{
		if (!TryCreateNewItem(out var item))
			throw new PoolExhaustedException(typeof(T), _options.MaxCapacity);

		return item;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private T RentCore()
	{
		while (_available.TryPop(out var item))
		{
			if (!_options.ValidateOnRent || _policy.Validate(item))
			{
				IncrementRented();
				NotifyRent(item);
				return item;
			}

			DiscardItem(item);
		}

		return CreateNewItem();
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void DiscardItem(T item)
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
			var item = _policy.Create();
			_available.Push(item);
			Interlocked.Increment(ref _totalCount);

			if (_options.TrackStatistics)
				Interlocked.Increment(ref _totalCreated);
		}
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
