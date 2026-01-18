using System;
using System.Numerics;

namespace Bezoro.GameSystems.Streaming;

/// <summary>
///     Configuration for the streaming system.
/// </summary>
public struct StreamingConfig
{
	/// <summary>
	///     Delegate that returns the current reference position for distance calculations.
	///     Typically the player/camera position.
	/// </summary>
	public Func<Vector3> GetReferencePosition;

	/// <summary>
	///     Distance at which entities should stream in.
	///     Entities closer than this distance will be streamed in.
	/// </summary>
	public float StreamInDistance;

	/// <summary>
	///     Distance at which entities should stream out.
	///     Should be greater than <see cref="StreamInDistance" /> for hysteresis to prevent flickering.
	/// </summary>
	public float StreamOutDistance;

	/// <summary>
	///     Maximum number of entities to process per iteration.
	///     Helps spread processing load across multiple frames.
	/// </summary>
	public int MaxPerFrame;

	/// <summary>
	///     Delay in milliseconds between processing iterations.
	/// </summary>
	public int FrameDelayMs;
}
