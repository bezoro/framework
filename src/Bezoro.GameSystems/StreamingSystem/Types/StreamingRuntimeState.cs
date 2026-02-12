namespace Bezoro.GameSystems.StreamingSystem.Types;

/// <summary>
///     Internal runtime resource for streaming iteration state.
/// </summary>
public sealed class StreamingRuntimeState
{
	/// <summary>
	///     Gets or sets the next global streamable index to evaluate.
	/// </summary>
	public int NextEntityIndex { get; set; }
}
