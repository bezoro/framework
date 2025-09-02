namespace Bezoro.UCI.Domain;

internal static class ProcessArgs
{
	// Windows cmd.exe switches
	public const string CmdExecute = "/c";
	public const string CmdKeep    = "/k";

	// Common commands
	public const string Echo      = "echo";
	public const string EchoEmpty = "echo.";
	public const string Exit      = "exit";

	// Operators
	public const string Chain = "&";

	// Redirection
	public const string StdOutToStdErr = "1>&2";
	public const string StdErrToStdOut = "2>&1";

	// Common numeric args
	public const string Zero = "0";
	public const string One  = "1";
}
