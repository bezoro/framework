namespace Bezoro.Core.Logging
{
	public class NullLogger : ILogger
	{
	#region Interface Implementations

		public void Log(Logger.LogEntry log) { }
		public void LogSuccess(Logger.LogEntry log) { }
		public void LogWarning(Logger.LogEntry log) { }
		public void LogError(Logger.LogEntry log) { }
		public void LogException(Logger.LogEntry log) { }

	#endregion
	}
}
