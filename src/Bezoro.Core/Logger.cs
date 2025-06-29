using System;
using System.Diagnostics;
using Bezoro.Core.Logging;

namespace Bezoro.Core
{
	public static class Logger
	{
		public static event Action<LogLevel, string> OnLog;

		[Conditional("DEBUG")]
		public static void LogError(string message) => Log(LogLevel.Error, message);

		[Conditional("DEBUG")]
		public static void LogException(string message) => Log(LogLevel.Exception, message);

		[Conditional("DEBUG")]
		public static void LogInfo(string message) => Log(LogLevel.Info, message);

		[Conditional("DEBUG")]
		public static void LogSuccess(string message) => Log(LogLevel.Success, message);

		[Conditional("DEBUG")]
		public static void LogWarning(string message) => Log(LogLevel.Warning, message);

		[Conditional("DEBUG")]
		private static void Log(LogLevel level, string message)
		{
			OnLog?.Invoke(level, message);
		}
	}
}
