namespace Bezoro.Core.Abstractions;

/// <summary>
///     Optional interface for objects that can manage their own pooling lifecycle.
///     Objects implementing this interface receive callbacks during pool operations.
/// </summary>
public interface IPooledObject
{
	/// <summary>
	///     Called when the object is about to be returned to the pool.
	///     Implementations should reset their state here.
	/// </summary>
	/// <returns><c>true</c> if the object is valid for reuse; <c>false</c> to discard.</returns>
	bool OnReturn();

	/// <summary>
	///     Called when the object is rented from the pool.
	/// </summary>
	void OnRent();
}
