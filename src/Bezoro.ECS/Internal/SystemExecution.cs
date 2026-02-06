namespace Bezoro.ECS.Internal;

internal readonly struct SystemExecution
{
	public SystemExecution(SystemState state, float deltaTime)
	{
		State     = state;
		DeltaTime = deltaTime;
	}

	public float DeltaTime { get; }

	public SystemState State { get; }
}
