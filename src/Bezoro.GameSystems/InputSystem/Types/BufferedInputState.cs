namespace Bezoro.GameSystems.InputSystem.Types;

internal readonly struct BufferedInputState(
	float moveX,
	float moveY,
	float moveZ,
	ulong sequence,
	double receivedAtSeconds)
{
	public float MoveX { get; } = moveX;

	public float MoveY { get; } = moveY;

	public float MoveZ { get; } = moveZ;

	public ulong Sequence { get; } = sequence;

	public double ReceivedAtSeconds { get; } = receivedAtSeconds;
}
