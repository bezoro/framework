namespace Bezoro.GameSystems.ActivationSystem.Types;

/// <summary>
///     ECS request component that cancels a pending activation entry by handle.
/// </summary>
public readonly struct ActivationCancellationRequest
{
	/// <summary>
	///     Initializes a cancellation request.
	/// </summary>
	/// <param name="handle">Target activation handle.</param>
	public ActivationCancellationRequest(ActivationHandle handle)
	{
		Handle = handle;
	}

	/// <summary>
	///     Gets the target activation handle.
	/// </summary>
	public ActivationHandle Handle { get; }
}
