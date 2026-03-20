namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Enumerates the coarse-grained activity states tracked for the engine.
/// </summary>
public enum EngineActivity
{
	/// <summary>Idle; engine is not searching or pondering.</summary>
	Idle = 0,

	/// <summary>Engine is actively searching.</summary>
	Searching = 1,

	/// <summary>Engine is pondering (analyzing at opponent's turn).</summary>
	Pondering = 2
}
