using System.Diagnostics;
using System.Runtime.InteropServices;
using Bezoro.Chess.UCI.Protocol.Domain.Common.Helpers;
using System.Text;
using static Bezoro.Chess.UCI.Protocol.Tests.TestHelpers.TestDataBuilders;

namespace Bezoro.Chess.UCI.Protocol.Tests.Domain;

/// <summary>Real child processes with identical byte output and lifecycle behavior on every test platform.</summary>
internal static class ProcessUciTransportTestHelpers
{
	public static ProcessUciTransportBuilder ExitingProcess() => TestProcess("exit");

	public static ProcessUciTransportBuilder RunningProcess() => TestProcess("wait-kill");

	public static ProcessUciTransportBuilder ExitOnInputProcess() => TestProcess("wait-input");

	public static ProcessUciTransportBuilder OutputProcess(params string[] lines) =>
		TestProcess("stdout", Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Join(Environment.NewLine, lines) + Environment.NewLine)), "exit");

	public static ProcessUciTransportBuilder ErrorOutputProcess(string line, bool waitForInput = false) =>
		TestProcess("stderr", Convert.ToBase64String(Encoding.UTF8.GetBytes(line + Environment.NewLine)), waitForInput ? "wait" : "exit");

	internal static Process StartChildProcess(params string[] arguments)
	{
		var command = TestProcessCommand(arguments);
		var process = new Process
		{
			StartInfo = ProcessHelper.CreateProcessStartInfo(command.Host, command.Arguments,
				Environment.CurrentDirectory, true, Encoding.UTF8, Encoding.UTF8),
			EnableRaisingEvents = true
		};
		try
		{
			if (!process.Start()) throw new InvalidOperationException("The test child process did not start.");
			return process;
		}
		catch
		{
			process.Dispose();
			throw;
		}
	}

	private static ProcessUciTransportBuilder TestProcess(params string[] arguments)
	{
		var command = TestProcessCommand(arguments);
		return Transport().WithPath(command.Host).WithArguments(command.Arguments)
			.WithSendQuitOnStop(false).WithSendQuitOnDispose(false);
	}

	private static (string Host, string[] Arguments) TestProcessCommand(string[] arguments)
	{
		string? host = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
		if (string.IsNullOrEmpty(host) || !File.Exists(host))
			host = Path.GetFullPath(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", "..",
				OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet"));
		if (!File.Exists(host)) throw new InvalidOperationException($"The .NET test host was not found at {host}.");

		string runtimeConfig = Path.ChangeExtension(typeof(ProcessUciTransportTestHelpers).Assembly.Location, ".runtimeconfig.json");
		string child = Path.Combine(AppContext.BaseDirectory, "Bezoro.Chess.UCI.Protocol.TestProcess.dll");
		return (host, ["exec", "--runtimeconfig", runtimeConfig, child, .. arguments]);
	}
}
