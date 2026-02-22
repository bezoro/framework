using Bezoro.ECS.Types;

namespace Bezoro.ECS.Abstractions;

/// <summary>
///     Defines a source that provides snapshot payloads for world restoration.
/// </summary>
public interface IWorldSnapshotReader
{
	/// <summary>
	///     Returns the snapshot payload to restore.
	/// </summary>
	/// <returns>Snapshot data suitable for <see cref="Services.World.RestoreSnapshot" />.</returns>
	WorldSnapshot Read();
}
