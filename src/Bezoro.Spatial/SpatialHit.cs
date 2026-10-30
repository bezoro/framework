using System.Numerics;
using Bezoro.Core.Collections;

namespace Bezoro.Spatial;

/// <summary>A copied point, payload, and process-local handle returned by a spatial index.</summary>
/// <typeparam name="T">The application's payload type.</typeparam>
public readonly struct SpatialHit<T>
{
    internal SpatialHit(SlotHandle handle, Vector2 position, T value)
    {
        Handle = handle;
        Position = position;
        Value = value;
    }

    /// <summary>Gets the entry's identity. Its liveness must be checked against the owning index.</summary>
    public SlotHandle Handle { get; }
    /// <summary>Gets the point at the time it was copied.</summary>
    public Vector2 Position { get; }
    /// <summary>Gets a copied payload. Reference payloads still refer to the application's objects.</summary>
    public T Value { get; }
}
