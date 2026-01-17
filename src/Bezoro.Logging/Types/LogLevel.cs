namespace Bezoro.Logging.Types;

/// <summary>
///     Defines the severity level for a log message.
/// </summary>
public enum LogLevel
{
    /// <summary>
    ///     Informational messages used for general output.
    /// </summary>
    Info,

    /// <summary>
    ///     Warnings that indicate a potential issue or non-critical problem.
    /// </summary>
    Warning,

    /// <summary>
    ///     Errors indicating a problem that has occurred but does not stop execution.
    /// </summary>
    Error,

    /// <summary>
    ///     Exceptions representing severe errors or unexpected failures.
    /// </summary>
    Exception,

    /// <summary>
    ///     Success notifications, used to indicate successful operations.
    /// </summary>
    Success
}
