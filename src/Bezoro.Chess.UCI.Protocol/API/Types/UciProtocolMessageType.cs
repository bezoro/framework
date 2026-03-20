namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Identifies the kind of parsed UCI protocol message.
/// </summary>
public enum UciProtocolMessageType
{
	/// <summary>
	///     An <c>id ...</c> message.
	/// </summary>
	Id,

	/// <summary>
	///     An <c>option ...</c> message.
	/// </summary>
	Option,

	/// <summary>
	///     A <c>uciok</c> message.
	/// </summary>
	UciOk,

	/// <summary>
	///     A <c>readyok</c> message.
	/// </summary>
	ReadyOk,

	/// <summary>
	///     An <c>info ...</c> message.
	/// </summary>
	Info,

	/// <summary>
	///     A <c>bestmove ...</c> message.
	/// </summary>
	BestMove,

	/// <summary>
	///     A <c>copyprotection ...</c> message.
	/// </summary>
	CopyProtection,

	/// <summary>
	///     A <c>registration ...</c> message.
	/// </summary>
	Registration
}
