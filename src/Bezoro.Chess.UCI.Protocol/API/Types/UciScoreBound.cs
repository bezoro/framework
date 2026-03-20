namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Represents the optional bound qualifier attached to a UCI score.
/// </summary>
public enum UciScoreBound
{
	/// <summary>
	///     The score is exact.
	/// </summary>
	Exact,

	/// <summary>
	///     The score is a lower bound.
	/// </summary>
	Lower,

	/// <summary>
	///     The score is an upper bound.
	/// </summary>
	Upper
}
