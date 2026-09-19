using Bezoro.ECS.Services;

namespace Bezoro.ECS.Types;

/// <summary>
///     Provides context for a system update execution.
/// </summary>
public readonly struct SystemContext
{
	/// <summary>
	///     Initializes a new system update context with the canonical command stream.
	/// </summary>
	/// <param name="deltaTime">The elapsed time passed to this system update.</param>
	/// <param name="stage">The stage executing this context.</param>
	/// <param name="world">The world instance executing this context.</param>
	/// <param name="commandStream">The command stream for deferred structural changes.</param>
	/// <exception cref="ArgumentNullException"><paramref name="commandStream" /> is <see langword="null" />.</exception>
	public SystemContext(float deltaTime, Stage stage, World world, CommandStream commandStream)
	{
		CommandStream = commandStream ?? throw new ArgumentNullException(nameof(commandStream));
		DeltaTime = deltaTime;
		Stage = stage;
		World = world;
	}

	/// <summary>
	///     Initializes a new system update context by forwarding a compatibility command buffer to the canonical command stream constructor.
	/// </summary>
	/// <param name="deltaTime">The elapsed time passed to this system update.</param>
	/// <param name="stage">The stage executing this context.</param>
	/// <param name="world">The world instance executing this context.</param>
	/// <param name="commands">The compatibility command buffer for deferred structural changes.</param>
	/// <exception cref="ArgumentNullException">The command buffer does not contain a command stream.</exception>
	#pragma warning disable CS0618
	[Obsolete("Use SystemContext(float, Stage, World, CommandStream) instead.")]
	public SystemContext(float deltaTime, Stage stage, World world, CommandBuffer commands)
		: this(deltaTime, stage, world, (CommandStream)commands) { }
	#pragma warning restore CS0618

	/// <summary>
	///     Gets the canonical command stream for deferred structural changes.
	/// </summary>
	public CommandStream CommandStream { get; }

	/// <summary>
	///     Gets a retained compatibility wrapper over <see cref="CommandStream" />.
	/// </summary>
	#pragma warning disable CS0618
	[Obsolete("Use SystemContext.CommandStream instead.")]
	public CommandBuffer Commands => new(CommandStream);
	#pragma warning restore CS0618

	/// <summary>
	///     Gets the elapsed time passed to this system update.
	/// </summary>
	public float DeltaTime { get; }

	/// <summary>
	///     Gets the stage currently being executed.
	/// </summary>
	public Stage Stage { get; }

	/// <summary>
	///     Gets the world instance executing this context.
	/// </summary>
	public World World { get; }
}
