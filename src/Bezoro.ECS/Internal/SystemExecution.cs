namespace Bezoro.ECS.Internal;

internal readonly struct SystemExecution(SystemState state, float deltaTime)
{
	public float DeltaTime { get; } = deltaTime;

	public SystemState State { get; } = state;
}
