namespace Bezoro.ECS.Types;

/// <summary>
///     Provides context for a system update execution.
/// </summary>
/// <remarks>
///     Initializes a new instance of the <see cref="SystemContext" /> struct.
/// </remarks>
/// <param name="deltaTime">The elapsed time passed to this system update.</param>
/// <param name="stage">The stage executing this context.</param>
/// <param name="commands">The command buffer for deferred structural changes.</param>
public readonly struct SystemContext(float deltaTime, Stage stage, CommandBuffer commands)
{
	/// <summary>
	///     Gets the command buffer for deferred structural changes.
	/// </summary>
	public CommandBuffer Commands { get; } = commands;

	/// <summary>
	///     Gets the elapsed time passed to this system update.
	/// </summary>
	public float DeltaTime { get; } = deltaTime;

	/// <summary>
	///     Gets the stage currently being executed.
	/// </summary>
	public Stage Stage { get; } = stage;
}
