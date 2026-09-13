namespace Bezoro.ECS.Types;

/// <summary>
///     Lightweight transform payload passed through engine adapters.
/// </summary>
/// <param name="x">Position on the x-axis.</param>
/// <param name="y">Position on the y-axis.</param>
/// <param name="z">Position on the z-axis.</param>
public readonly struct TransformProxy(float x, float y, float z)
{
	/// <summary>Gets the position on the x-axis.</summary>
	public float X { get; } = x;

	/// <summary>Gets the position on the y-axis.</summary>
	public float Y { get; } = y;

	/// <summary>Gets the position on the z-axis.</summary>
	public float Z { get; } = z;
}
