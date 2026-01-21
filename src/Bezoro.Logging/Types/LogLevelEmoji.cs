namespace Bezoro.Logging.Types;

/// <summary>
///     Provides utility methods to get emoji representations for log levels.
/// </summary>
public static class LogLevelEmoji
{
	private static readonly Dictionary<LogLevel, string> LevelToEmoji = new()
	{
		{ LogLevel.Info, "ℹ️" },
		{ LogLevel.Warning, "⚠️" },
		{ LogLevel.Error, "❌" },
		{ LogLevel.Exception, "🆘" },
		{ LogLevel.Success, "✅" }
	};

	/// <summary>
	///     Gets the emoji representation for a specific log level.
	/// </summary>
	/// <param name="level">The log level.</param>
	/// <returns>
	///     Emoji string for the specified level, or <c>❓</c> if the level is not recognized.
	/// </returns>
	public static string GetEmoji(LogLevel level) =>
		LevelToEmoji.GetValueOrDefault(level, "❓");
}
