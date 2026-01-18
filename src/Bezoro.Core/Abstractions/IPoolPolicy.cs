namespace Bezoro.Core.Abstractions;

/// <summary>
///     Defines the lifecycle and validation policy for pooled objects.
/// </summary>
/// <typeparam name="T">The type of objects managed by the pool.</typeparam>
public interface IPoolPolicy<T> where T : class
{
	/// <summary>
	///     Creates a new instance of the pooled object type.
	/// </summary>
	/// <returns>A new instance of <typeparamref name="T" />.</returns>
	T Create();

	/// <summary>
	///     Resets an object to a clean state before returning it to the pool.
	/// </summary>
	/// <param name="item">The object to reset.</param>
	/// <returns><c>true</c> if the object was successfully reset and should be pooled; <c>false</c> to discard.</returns>
	bool Reset(T item);

	/// <summary>
	///     Validates an object before it is rented out.
	/// </summary>
	/// <param name="item">The object to validate.</param>
	/// <returns><c>true</c> if the object is valid for use; <c>false</c> to discard and create a new one.</returns>
	bool Validate(T item);

	/// <summary>
	///     Called when an object is being discarded and will not be returned to the pool.
	/// </summary>
	/// <param name="item">The object being discarded.</param>
	void OnDiscard(T item);
}
