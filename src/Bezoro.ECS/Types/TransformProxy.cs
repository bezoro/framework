namespace Bezoro.ECS.Types;

/// <summary>
///     Lightweight transform payload passed through engine adapters.
/// </summary>
public readonly struct TransformProxy
{
	public TransformProxy(float x, float y, float z)
	{
		X = x;
		Y = y;
		Z = z;
	}

	public float X { get; }
	public float Y { get; }
	public float Z { get; }
}
