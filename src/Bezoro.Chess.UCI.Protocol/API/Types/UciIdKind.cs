namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Identifies which <c>id</c> payload an engine reported.
/// </summary>
public enum UciIdKind
{
	/// <summary>
	///     The engine name reported by <c>id name ...</c>.
	/// </summary>
	Name,

	/// <summary>
	///     The engine author reported by <c>id author ...</c>.
	/// </summary>
	Author
}
