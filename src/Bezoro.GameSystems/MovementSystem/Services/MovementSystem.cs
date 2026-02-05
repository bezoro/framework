using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Types;
using Bezoro.GameSystems.MovementSystem.Types;

namespace Bezoro.GameSystems.MovementSystem.Services;

/// <summary>
///     Updates position components using their velocity each tick.
/// </summary>
/// <remarks>
///     Iterates chunked component arrays sequentially for cache-friendly processing.
/// </remarks>
public sealed class MovementSystem : ISystem
{
	/// <summary>
	///     Initializes a new instance of the <see cref="MovementSystem" /> class.
	/// </summary>
	public MovementSystem() : this(SystemUpdateSettings.EveryFrame) { }

	/// <summary>
	///     Initializes a new instance of the <see cref="MovementSystem" /> class.
	/// </summary>
	/// <param name="updateSettings">Update frequency configuration.</param>
	public MovementSystem(SystemUpdateSettings updateSettings)
	{
		UpdateSettings = updateSettings;
		Accesses       = [ComponentAccess.Write<Position>(), ComponentAccess.Read<Velocity>()];
	}

	/// <summary>
	///     Gets the component access requirements for this system.
	/// </summary>
	public ComponentAccess[] Accesses { get; }

	/// <summary>
	///     Gets the update settings that control how often this system runs.
	/// </summary>
	public SystemUpdateSettings UpdateSettings { get; }

	/// <summary>
	///     Performs the movement update for all matching entities.
	/// </summary>
	/// <param name="world">The world context this system operates on.</param>
	/// <param name="context">The update context for this execution.</param>
	public void Update(IWorld world, in SystemContext context)
	{
		float deltaTime = context.DeltaTime;
		if (deltaTime == 0f) return;

		foreach (var chunk in world.Query().With<Position>().With<Velocity>())
		{
			var positions  = chunk.Components<Position>();
			var velocities = chunk.Components<Velocity>();

			for (var i = 0; i < chunk.Count; i++)
			{
				positions[i].X += velocities[i].X * deltaTime;
				positions[i].Y += velocities[i].Y * deltaTime;
				positions[i].Z += velocities[i].Z * deltaTime;
			}
		}
	}
}
