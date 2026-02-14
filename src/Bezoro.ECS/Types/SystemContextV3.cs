using Bezoro.ECS.Services;

namespace Bezoro.ECS.Types;

/// <summary>
/// Provides context for a <see cref="Abstractions.ISystemV3" /> update.
/// </summary>
public readonly struct SystemContextV3(float deltaTime, WorldV3 world, CommandStreamV3 commands)
{
	/// <summary>
	/// Gets the world instance being updated.
	/// </summary>
	public WorldV3 World { get; } = world;

	/// <summary>
	/// Gets the deferred command stream owned by this system execution.
	/// </summary>
	public CommandStreamV3 Commands { get; } = commands;

	/// <summary>
	/// Gets the elapsed time passed to this system update.
	/// </summary>
	public float DeltaTime { get; } = deltaTime;
}
