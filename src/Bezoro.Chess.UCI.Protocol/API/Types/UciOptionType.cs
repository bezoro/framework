namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Represents the supported UCI option kinds advertised by an engine.
/// </summary>
public enum UciOptionType
{
	Check,
	Spin,
	String,
	Button,
	Combo
}
