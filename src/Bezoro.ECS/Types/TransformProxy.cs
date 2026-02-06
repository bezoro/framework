namespace Bezoro.ECS.Types;

/// <summary>
///     Lightweight transform payload passed through engine adapters.
/// </summary>
public readonly struct TransformProxy(float x, float y, float z)
{
	public float X { get; } = x;
	public float Y { get; } = y;
	public float Z { get; } = z;
}
