namespace Bezoro.UCI.Domain;

internal static class ProcessArgs
{
	// Operators
	public const string CHAIN = "&";
	// Windows cmd.exe switches
	public const string CMD_EXECUTE = "/c";
	public const string CMD_KEEP    = "/k";

	// Common commands
	public const string ECHO               = "echo";
	public const string ECHO_EMPTY         = "echo.";
	public const string EXIT               = "exit";
	public const string ONE                = "1";
	public const string STD_ERR_TO_STD_OUT = "2>&1";

	// Redirection
	public const string STD_OUT_TO_STD_ERR = "1>&2";

	// Common numeric args
	public const string ZERO = "0";
}
