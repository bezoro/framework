using Bezoro.ECS.Services;

namespace Bezoro.ECS.Types;

/// <summary>
///     Provides context for a system update execution.
/// </summary>
/// <remarks>
///     Initializes a new instance of the <see cref="SystemContext" /> struct.
/// </remarks>
/// <param name="deltaTime">The elapsed time passed to this system update.</param>
/// <param name="stage">The stage executing this context.</param>
/// <param name="world">The world instance executing this context.</param>
/// <param name="commands">The command stream for deferred structural changes.</param>
public readonly struct SystemContext(float deltaTime, Stage stage, World world, CommandStream commands)
{
	/// <summary>
	///     Gets the world instance executing this context.
	/// </summary>
	public World World { get; } = world;

	/// <summary>
	///     Gets the command stream for deferred structural changes.
	/// </summary>
	public CommandStream Commands { get; } = commands;

	/// <summary>
	///     Gets the elapsed time passed to this system update.
	/// </summary>
	public float DeltaTime { get; } = deltaTime;

	/// <summary>
	///     Gets the stage currently being executed.
	/// </summary>
	public Stage Stage { get; } = stage;
}
