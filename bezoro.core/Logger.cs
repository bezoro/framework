using System;
using System.Runtime.ExceptionServices;
using Bezoro.Core.Logging;

namespace Bezoro.Core
{
	public static class Logger
	{
		public static ILogger LoggerInstance { get; private set; } = new NullLogger();

		public static void Log_Error(string message, object contextObject = null, string category = "Uncategorized")
		{
			var log = new LogEntry(message, contextObject, LogEntry.LogType.Error, category);

			LoggerInstance?.LogError(log);
		}

		public static void Log_Exception(Exception exception, bool throwException = true, object contextObject = null)
		{
			var log = new LogEntry(
				exception.ToString(), contextObject, LogEntry.LogType.Exception, "Uncategorized");

			LoggerInstance?.LogException(log);

			if (throwException)
				ExceptionDispatchInfo.Capture(exception).Throw();
		}

		public static void LogInfo(string message, object contextObject = null, string category = "Uncategorized")
		{
			var log = new LogEntry(message, contextObject, LogEntry.LogType.Info, category);

			LoggerInstance?.Log(log);
		}

		public static void LogWarning(string message, object contextObject = null, string category = "Uncategorized")
		{
			var log = new LogEntry(message, contextObject, LogEntry.LogType.Warning, category);

			LoggerInstance?.LogWarning(log);
		}

		public static void LogSuccess(string message, object contextObject = null, string category = "Uncategorized")
		{
			var log = new LogEntry(message, contextObject, LogEntry.LogType.Success, category);

			LoggerInstance?.LogSuccess(log);
		}

		public static void Set_Logger(ILogger logger)
		{
			LoggerInstance = logger;
			LogSuccess($"Logger set to {LoggerInstance.GetType().Name}.");
		}

		public class LogEntry
		{
			public LogEntry(string message, object contextObject, LogType type, string category)
			{
				Type           = type;
				Category       = category;
				Message        = message;
				Context_Object = contextObject;
			}

			public string Category       { get; }
			public object Context_Object { get; }

			public string  Message { get; }
			public LogType Type    { get; }

			public enum LogType
			{
				Info,
				Warning,
				Error,
				Success,
				Exception
			}
		}
	}
}
