namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Represents the supported UCI option kinds advertised by an engine.
/// </summary>
public enum UciOptionType
{
	/// <summary>
	///     Boolean option represented by checked or unchecked state.
	/// </summary>
	Check,

	/// <summary>
	///     Numeric option with minimum and maximum bounds.
	/// </summary>
	Spin,

	/// <summary>
	///     Free-form string option.
	/// </summary>
	String,

	/// <summary>
	///     Fire-and-forget button option with no persisted value.
	/// </summary>
	Button,

	/// <summary>
	///     Enumerated option with a fixed set of legal values.
	/// </summary>
	Combo
}
