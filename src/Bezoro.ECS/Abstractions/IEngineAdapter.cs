using Bezoro.ECS.Types;

namespace Bezoro.ECS.Abstractions;

/// <summary>
///     Adapter used to bridge world data to an engine runtime.
/// </summary>
public interface IEngineAdapter
{
	/// <summary>
	///     Gets the elapsed simulation time since the previous engine update.
	/// </summary>
	/// <returns>The elapsed time, in seconds.</returns>
	float           GetDeltaTime();

	/// <summary>
	///     Polls the engine's current input state.
	/// </summary>
	/// <returns>A framework-neutral input snapshot.</returns>
	InputStateProxy PollInput();

	/// <summary>
	///     Applies framework transform data to the corresponding engine entities.
	/// </summary>
	/// <param name="entities">Entities whose transforms should be synchronized.</param>
	/// <param name="transforms">Transforms aligned by index with <paramref name="entities" />.</param>
	void            SyncTransformsToEngine(ReadOnlySpan<Entity> entities, ReadOnlySpan<TransformProxy> transforms);
}
