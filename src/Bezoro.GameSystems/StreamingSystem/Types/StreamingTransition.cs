namespace Bezoro.GameSystems.StreamingSystem.Types;

/// <summary>
///     Streaming transition emitted by <see cref="Services.StreamingSystem" />.
/// </summary>
public enum StreamingTransition
{
	/// <summary>The entity entered the configured streaming range.</summary>
	StreamedIn,

	/// <summary>The entity left the configured streaming range.</summary>
	StreamedOut
}
