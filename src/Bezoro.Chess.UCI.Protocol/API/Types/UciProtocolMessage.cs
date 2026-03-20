namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Immutable envelope for a parsed line of UCI protocol output.
/// </summary>
public readonly record struct UciProtocolMessage(
	UciProtocolMessageType       Type,
	string                       RawLine,
	UciIdMessage?                Id = null,
	UciOptionMessage?            Option = null,
	UciInfoMessage?              Info = null,
	UciBestMoveMessage?          BestMove = null,
	UciCopyProtectionMessage?    CopyProtection = null,
	UciRegistrationMessage?      Registration = null,
	UciReadyOkMessage?           ReadyOk = null,
	UciUciOkMessage?             UciOk = null
)
{
	internal static UciProtocolMessage From(UciBestMoveMessage message) =>
		new(UciProtocolMessageType.BestMove, message.RawLine, BestMove: message);

	internal static UciProtocolMessage From(UciCopyProtectionMessage message) =>
		new(UciProtocolMessageType.CopyProtection, message.RawLine, CopyProtection: message);

	internal static UciProtocolMessage From(UciIdMessage message) =>
		new(UciProtocolMessageType.Id, message.RawLine, Id: message);

	internal static UciProtocolMessage From(UciInfoMessage message) =>
		new(UciProtocolMessageType.Info, message.RawLine, Info: message);

	internal static UciProtocolMessage From(UciOptionMessage message) =>
		new(UciProtocolMessageType.Option, message.RawLine, Option: message);

	internal static UciProtocolMessage From(UciReadyOkMessage message) =>
		new(UciProtocolMessageType.ReadyOk, message.RawLine, ReadyOk: message);

	internal static UciProtocolMessage From(UciRegistrationMessage message) =>
		new(UciProtocolMessageType.Registration, message.RawLine, Registration: message);

	internal static UciProtocolMessage From(UciUciOkMessage message) =>
		new(UciProtocolMessageType.UciOk, message.RawLine, UciOk: message);
}
