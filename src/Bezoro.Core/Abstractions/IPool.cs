using System.Diagnostics.CodeAnalysis;
using Bezoro.Core.Types;
using Bezoro.Core.Types.Pool;

namespace Bezoro.Core.Abstractions;

/// <summary>
///     Represents a thread-safe object pool that manages reusable instances of type <typeparamref name="T" />.
/// </summary>
/// <typeparam name="T">The type of objects managed by the pool.</typeparam>
public interface IPool<T> where T : class
{
	/// <summary>
	///     Gets the current number of available objects in the pool.
	/// </summary>
	int AvailableCount { get; }

	/// <summary>
	///     Gets the maximum capacity of the pool. Returns -1 if unbounded.
	/// </summary>
	int MaxCapacity { get; }

	/// <summary>
	///     Gets the total number of objects created by this pool (rented + available).
	/// </summary>
	int TotalCount { get; }

	/// <summary>
	///     Gets pool statistics and metrics for diagnostics.
	/// </summary>
	PoolStatistics Statistics { get; }

	/// <summary>
	///     Returns an object to the pool. The object is reset according to the pool policy.
	/// </summary>
	/// <param name="item">The object to return.</param>
	/// <returns><c>true</c> if the object was returned to the pool; <c>false</c> if discarded due to capacity or validation.</returns>
	bool Return(T item);

	/// <summary>
	///     Attempts to rent an object from the pool without blocking.
	/// </summary>
	/// <param name="item">The rented object if successful; otherwise, <c>null</c>.</param>
	/// <returns><c>true</c> if an object was successfully rented; otherwise, <c>false</c>.</returns>
	bool TryRent([NotNullWhen(true)] out T? item);

	/// <summary>
	///     Trims excess capacity from the pool based on the specified target utilization.
	/// </summary>
	/// <param name="targetUtilization">Target utilization percentage (0-100). Defaults to 90%.</param>
	/// <returns>The number of objects removed from the pool.</returns>
	int TrimExcess(Percent targetUtilization = default);

	/// <summary>
	///     Rents an object wrapped in a disposable handle for automatic return.
	/// </summary>
	/// <returns>A handle that returns the object when disposed.</returns>
	PooledObjectHandle<T> RentHandle();

	/// <summary>
	///     Rents an object from the pool. Creates a new instance if the pool is empty.
	/// </summary>
	/// <returns>A pooled object instance.</returns>
	/// <exception cref="PoolExhaustedException">
	///     Thrown when the pool is at capacity and cannot create new objects.
	/// </exception>
	T Rent();

	/// <summary>
	///     Asynchronously waits for an object with a timeout.
	/// </summary>
	/// <param name="timeout">Maximum time to wait for an object.</param>
	/// <param name="cancellationToken">Token to cancel the wait operation.</param>
	/// <returns>A task containing the object, or <c>null</c> if the operation timed out.</returns>
	ValueTask<T?> RentAsync(TimeSpan timeout, CancellationToken cancellationToken = default);

	/// <summary>
	///     Asynchronously waits for an object to become available when the pool is exhausted.
	/// </summary>
	/// <param name="cancellationToken">Token to cancel the wait operation.</param>
	/// <returns>A task that completes with a pooled object.</returns>
	/// <exception cref="OperationCanceledException">Thrown when cancellation is requested.</exception>
	ValueTask<T> RentAsync(CancellationToken cancellationToken = default);

	/// <summary>
	///     Clears all objects from the pool, optionally disposing them.
	/// </summary>
	/// <param name="disposeItems">If <c>true</c>, disposes items implementing <see cref="IDisposable" />.</param>
	void Clear(bool disposeItems = true);
}
