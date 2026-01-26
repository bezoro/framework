using System.Numerics;

namespace Bezoro.GameSystems.StreamingSystem;

/// <summary>
///     Interface for entities that can be streamed in and out based on distance from a reference point.
/// </summary>
public interface IStreamableEntity
{
	/// <summary>
	///     Gets whether this entity is currently streamed in.
	/// </summary>
	bool IsStreamedIn { get; }

	/// <summary>
	///     Gets the unique identifier for this entity.
	/// </summary>
	int EntityId { get; }

	/// <summary>
	///     Gets the current position used for streaming distance calculations.
	/// </summary>
	Vector3 StreamingPosition { get; }

	/// <summary>
	///     Called on the main thread when the entity should stream in.
	/// </summary>
	void OnStreamIn();

	/// <summary>
	///     Called on the main thread when the entity should stream out.
	/// </summary>
	void OnStreamOut();
}
