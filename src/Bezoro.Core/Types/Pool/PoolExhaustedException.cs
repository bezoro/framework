namespace Bezoro.Core.Types.Pool;

/// <summary>
///     Represents an error that occurs when a pool has reached its maximum capacity
///     and cannot provide additional objects.
/// </summary>
/// <remarks>
///     Initializes a new instance of the <see cref="PoolExhaustedException" /> class.
/// </remarks>
/// <param name="pooledType">The type of object that the pool manages.</param>
/// <param name="maxCapacity">The maximum capacity of the pool.</param>
public class PoolExhaustedException(Type pooledType, int maxCapacity) : Exception(
	$"Pool of '{pooledType.Name}' is exhausted. Maximum capacity: {maxCapacity}."
)
{
    /// <summary>
    ///     Gets the maximum capacity of the pool.
    /// </summary>
    public int MaxCapacity { get; } = maxCapacity;

    /// <summary>
    ///     Gets the type of object that the pool manages.
    /// </summary>
    public Type PooledType { get; } = pooledType;
}
