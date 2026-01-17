namespace Bezoro.Logging.Types;

/// <summary>
///     Defines visual styling for log output.
/// </summary>
public sealed class LogStyle
{
	/// <summary>
	///     Initializes a new instance of the <see cref="LogStyle" /> class with the specified visual styling options.
	/// </summary>
	/// <param name="color">The console color to use for this log style.</param>
	/// <param name="bold">Whether to render the text in bold. Default is <c>false</c>.</param>
	/// <param name="italic">Whether to render the text in italic. Default is <c>false</c>.</param>
	public LogStyle(ConsoleColor color, bool bold = false, bool italic = false)
	{
		Color  = color;
		Bold   = bold;
		Italic = italic;
	}

	/// <summary>
	///     Whether to render the text in bold.
	/// </summary>
	public bool Bold { get; init; }

	/// <summary>
	///     Whether to render the text in italic.
	/// </summary>
	public bool Italic { get; init; }

	/// <summary>
	///     The color hint for this log style.
	/// </summary>
	public ConsoleColor Color { get; init; }
}
