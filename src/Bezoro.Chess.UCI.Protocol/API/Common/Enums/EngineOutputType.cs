namespace Bezoro.Chess.UCI.Protocol.API.Common.Enums;

/// <summary>
///     Represents different types of engine output during search.
/// </summary>
public enum EngineOutputType
{
	/// <summary>
	///     A regular <c>info</c> search-status line.
	/// </summary>
	Info,

	/// <summary>
	///     A final <c>bestmove</c> line.
	/// </summary>
	BestMove,

	/// <summary>
	///     An <c>option name ...</c> capability advertisement line.
	/// </summary>
	Option,

	/// <summary>
	///     An engine identity line such as <c>id name</c> or <c>id author</c>.
	/// </summary>
	Id,

	/// <summary>
	///     A non-search status line such as <c>readyok</c> or <c>uciok</c>.
	/// </summary>
	Status,

	/// <summary>
	///     Output that does not match any known classification.
	/// </summary>
	Unknown
}
