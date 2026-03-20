namespace Bezoro.Chess.UCI.Protocol.Tests.Domain;

/// <summary>
///     Shared test helpers for ProcessUciTransport tests.
/// </summary>
internal static class ProcessUciTransportTestHelpers
{
	/// <summary>
	///     Resolves the path to cmd.exe on Windows for integration tests.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when cmd.exe cannot be found.</exception>
	public static string TryResolveCmdPath()
	{
		string systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
		if (!string.IsNullOrWhiteSpace(systemRoot))
		{
			string cmd = Path.Combine(systemRoot, "System32", "cmd.exe");
			if (File.Exists(cmd)) return cmd;
		}

		string  envCmd = Environment.ExpandEnvironmentVariables(@"%SystemRoot%\System32\cmd.exe");
		string? file   = File.Exists(envCmd) ? envCmd : null;
		if (file is null)
			throw new InvalidOperationException("Unable to resolve cmd.exe path");

		return file;
	}
}
