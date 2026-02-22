using Bezoro.ECS.Types;

namespace Bezoro.ECS.Abstractions;

/// <summary>
///     Defines a sink that receives a captured world snapshot.
/// </summary>
public interface IWorldSnapshotWriter
{
	/// <summary>
	///     Receives a captured snapshot payload.
	/// </summary>
	/// <param name="snapshot">Snapshot data captured from a <see cref="Services.World" /> instance.</param>
	void Write(in WorldSnapshot snapshot);
}
