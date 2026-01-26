using System;
using System.Numerics;
using System.Threading;

namespace Bezoro.GameSystems.StreamingSystem;

/// <summary>
///     Configuration for the streaming system.
/// </summary>
public readonly struct StreamingConfig(
	Func<Vector3>           getReferencePosition,
	float                   streamInDistance  = 100,
	float                   streamOutDistance = 120,
	int                     frameDelayMs      = 16,
	int                     maxPerFrame       = 50,
	SynchronizationContext? callbackContext   = null
)
{
	/// <summary>
	///     Distance at which entities should stream in.
	///     Entities closer than this distance will be streamed in.
	/// </summary>
	public readonly float StreamInDistance = streamInDistance;

	/// <summary>
	///     Distance at which entities should stream out.
	///     Should be greater than <see cref="StreamInDistance" /> for hysteresis to prevent flickering.
	/// </summary>
	public readonly float StreamOutDistance = streamOutDistance;
	/// <summary>
	///     Delegate that returns the current reference position for distance calculations.
	///     Called each processing iteration. Typically returns player/camera position.
	/// </summary>
	public readonly Func<Vector3> GetReferencePosition = getReferencePosition;

	/// <summary>
	///     Delay in milliseconds between processing iterations.
	/// </summary>
	public readonly int FrameDelayMs = frameDelayMs;

	/// <summary>
	///     Maximum number of entities to process per iteration.
	///     Helps spread processing load across multiple frames.
	/// </summary>
	public readonly int MaxPerFrame = maxPerFrame;

	/// <summary>
	///     Optional synchronization context for marshalling callbacks.
	///     When set, <see cref="IStreamableEntity.OnStreamIn" /> and <see cref="IStreamableEntity.OnStreamOut" />
	///     are posted to this context. When null (default), callbacks execute directly on the background thread.
	/// </summary>
	/// <example>
	///     // In Unity, capture on main thread during initialization:
	///     var mainThreadContext = SynchronizationContext.Current;
	///     // Then use in config:
	///     config.CallbackContext = mainThreadContext;
	/// </example>
	public readonly SynchronizationContext? CallbackContext = callbackContext;
}
