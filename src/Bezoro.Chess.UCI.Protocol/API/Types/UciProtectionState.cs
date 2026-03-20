namespace Bezoro.Chess.UCI.Protocol.API.Types;

/// <summary>
///     Represents the state reported by <c>copyprotection</c> or <c>registration</c> messages.
/// </summary>
public enum UciProtectionState
{
	/// <summary>
	///     The engine is still checking its registration or copy-protection state.
	/// </summary>
	Checking,

	/// <summary>
	///     The engine reported a successful registration or copy-protection state.
	/// </summary>
	Ok,

	/// <summary>
	///     The engine reported a registration or copy-protection error.
	/// </summary>
	Error
}
