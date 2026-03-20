namespace Bezoro.UCI.API.Types;

/// <summary>
///     Represents the payload for the UCI <c>register</c> command.
/// </summary>
public readonly record struct UciRegistration(bool Later, string? Name, string? Code)
{
	/// <summary>
	///     Builds a <c>register later</c> payload.
	/// </summary>
	public static UciRegistration LaterOnly() => new(true, null, null);

	/// <summary>
	///     Builds a <c>register name ... [code ...]</c> payload.
	/// </summary>
	public static UciRegistration WithCredentials(string name, string? code = null) =>
		new(false, name, code);
}
