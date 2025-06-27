using System;
using Bezoro.Core.Logging;

namespace Bezoro.Core
{
	public static class Logger
	{
		public static event Action<LogLevel, string> OnLog;

		public static void LogError(string message)     => Log(LogLevel.Error,     message);
		public static void LogException(string message) => Log(LogLevel.Exception, message);
		public static void LogInfo(string message)      => Log(LogLevel.Info,      message);
		public static void LogSuccess(string message)   => Log(LogLevel.Success,   message);
		public static void LogWarning(string message)   => Log(LogLevel.Warning,   message);

		private static void Log(LogLevel level, string message)
		{
			OnLog?.Invoke(level, message);
		}
	}
}
