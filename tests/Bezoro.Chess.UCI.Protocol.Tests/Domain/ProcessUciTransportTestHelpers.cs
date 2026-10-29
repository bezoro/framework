using static Bezoro.Chess.UCI.Protocol.Tests.TestHelpers.TestDataBuilders;

namespace Bezoro.Chess.UCI.Protocol.Tests.Domain;

/// <summary>Native child processes with equivalent behavior on Windows and POSIX hosts.</summary>
internal static class ProcessUciTransportTestHelpers
{
	public static ProcessUciTransportBuilder ExitingProcess() => ShellProcess("exit 0", "exit 0");

	public static ProcessUciTransportBuilder ExitOnInputProcess() =>
		ShellProcess("set /p input=&exit 0", "IFS= read -r input; exit 0");

	public static ProcessUciTransportBuilder OutputProcess(params string[] lines) => PrintingProcess(lines, stderr: false);

	public static ProcessUciTransportBuilder ErrorOutputProcess(string line) => PrintingProcess([line], stderr: true);

	private static ProcessUciTransportBuilder PrintingProcess(string[] lines, bool stderr)
	{
		string redirect = stderr ? " 1>&2" : string.Empty;
		string windowsCommand = "chcp 65001>nul&" + string.Join("&", lines.Select(line => "echo(" + EscapeCmdEcho(line) + redirect));
		string posixCommand = "printf '%s\\n' " + string.Join(" ", lines.Select(line => "'" + line.Replace("'", "'\\''") + "'")) + redirect;
		return ShellProcess(windowsCommand, posixCommand);
	}

	private static ProcessUciTransportBuilder ShellProcess(string windowsCommand, string posixCommand)
	{
		string path = OperatingSystem.IsWindows()
			? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe")
			: "/bin/sh";
		if (!File.Exists(path))
			throw new InvalidOperationException($"The platform command interpreter was not found at {path}.");

		return Transport().WithPath(path)
			.WithArguments(OperatingSystem.IsWindows() ? ["/d", "/c", windowsCommand] : ["-c", posixCommand])
			.WithSendQuitOnStop(false).WithSendQuitOnDispose(false);
	}

	private static string EscapeCmdEcho(string line) => line.Replace("^", "^^").Replace("&", "^&")
		.Replace("|", "^|").Replace("<", "^<").Replace(">", "^>").Replace("(", "^(").Replace(")", "^)");
}
