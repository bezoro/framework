namespace Bezoro.Logging.Types;

/// <summary>
///     Defines visual styling for log output.
/// </summary>
/// <remarks>
///     Initializes a new instance of the <see cref="LogStyle" /> class with the specified visual styling options.
/// </remarks>
/// <param name="color">The console color to use for this log style.</param>
/// <param name="bold">Whether to render the text in bold. Default is <c>false</c>.</param>
/// <param name="italic">Whether to render the text in italic. Default is <c>false</c>.</param>
public sealed class LogStyle(ConsoleColor color, bool bold = false, bool italic = false)
{
    /// <summary>
    ///     Whether to render the text in bold.
    /// </summary>
    public bool Bold { get; init; } = bold;

    /// <summary>
    ///     Whether to render the text in italic.
    /// </summary>
    public bool Italic { get; init; } = italic;

    /// <summary>
    ///     The color hint for this log style.
    /// </summary>
    public ConsoleColor Color { get; init; } = color;
}
