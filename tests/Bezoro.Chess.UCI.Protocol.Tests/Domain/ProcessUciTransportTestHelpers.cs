using System.Runtime.InteropServices;
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

	private static ProcessUciTransportBuilder TestProcess(params string[] arguments)
	{
		string? host = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
		if (string.IsNullOrEmpty(host) || !File.Exists(host))
			host = Path.GetFullPath(Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "..", "..", "..",
				OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet"));
		if (!File.Exists(host)) throw new InvalidOperationException($"The .NET test host was not found at {host}.");

		string runtimeConfig = Path.ChangeExtension(typeof(ProcessUciTransportTestHelpers).Assembly.Location, ".runtimeconfig.json");
		string child = Path.Combine(AppContext.BaseDirectory, "Bezoro.Chess.UCI.Protocol.TestProcess.dll");
		return Transport().WithPath(host)
			.WithArguments(["exec", "--runtimeconfig", runtimeConfig, child, .. arguments])
			.WithSendQuitOnStop(false).WithSendQuitOnDispose(false);
	}
}
