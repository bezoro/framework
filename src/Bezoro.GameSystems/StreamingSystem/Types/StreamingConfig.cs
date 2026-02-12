using System.Numerics;

namespace Bezoro.GameSystems.StreamingSystem.Types;

/// <summary>
///     Shared streaming configuration consumed by <see cref="Services.StreamingSystem" />.
/// </summary>
public struct StreamingConfig
{
	/// <summary>
	///     Gets or sets the reference position used for distance checks.
	/// </summary>
	public Vector3 ReferencePosition;

	/// <summary>
	///     Gets or sets the distance threshold for transitioning out to streamed-in.
	/// </summary>
	public float StreamInDistance;

	/// <summary>
	///     Gets or sets the distance threshold for transitioning out of streamed-in.
	/// </summary>
	public float StreamOutDistance;

	/// <summary>
	///     Gets or sets the maximum entities processed per tick.
	///     Zero or negative values process all matching entities.
	/// </summary>
	public int MaxEntitiesPerTick;
}
