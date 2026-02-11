using Bezoro.Core.Helpers;
using Bezoro.ECS.Abstractions;
using Bezoro.ECS.Attributes;
using Bezoro.ECS.Types;
using Bezoro.GameSystems.MovementSystem.Types;

namespace Bezoro.GameSystems.MovementSystem.Services;

/// <summary>
///     Updates position components using their velocity each tick.
/// </summary>
/// <remarks>
///     Uses typed query iteration for cache-friendly sequential processing.
/// </remarks>
[Writes<Position>]
[Reads<Velocity>]
public sealed class MovementSystem : ISystem
{
	/// <summary>
	///     Initializes a new instance of the <see cref="MovementSystem" /> class.
	/// </summary>
	public MovementSystem() : this(SystemUpdateSettings.EveryTick) { }

	/// <summary>
	///     Initializes a new instance of the <see cref="MovementSystem" /> class.
	/// </summary>
	/// <param name="updateSettings">Tick frequency configuration.</param>
	public MovementSystem(SystemUpdateSettings updateSettings)
	{
		UpdateSettings = updateSettings;
	}

	public Stage Stage => Stage.Tick;

	public SystemLoopPhase LoopPhase => SystemLoopPhase.FixedTick;

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
		if (FloatComparer.IsZero(deltaTime)) return;

		world.Query().All<Position>().All<Velocity>().ForEach((ref Position position, in Velocity velocity) =>
			{
				position.X += velocity.X * deltaTime;
				position.Y += velocity.Y * deltaTime;
				position.Z += velocity.Z * deltaTime;
			}
		);
	}
}
