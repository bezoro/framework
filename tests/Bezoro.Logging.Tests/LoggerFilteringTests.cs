using Bezoro.Logging.Types;
using FluentAssertions;

namespace Bezoro.Logging.Tests;

public sealed class LoggerFilteringTests
{
	[Fact]
	public void Log_WhenLoggingIsDisabled_ShouldNotDispatch()
	{
		using var settings = new LoggerSettingsScope();
		LoggerSettings.Enabled = false;
		Capture(() => Logger.Log("hidden")).Should().BeEmpty();
	}

	[Fact]
	public void Log_WhenLevelIsBelowMinimum_ShouldNotDispatch()
	{
		using var settings = new LoggerSettingsScope();
		Logger.MinimumLevel = LogLevel.Error;
		Capture(() => Logger.Log("hidden", LogLevel.Warning)).Should().BeEmpty();
	}

	[Fact]
	public void Log_WhenCategoryIsMuted_ShouldNotDispatch()
	{
		using var settings = new LoggerSettingsScope();
		LoggerSettings.MutedCategories.Add(LogCategory.Test);
		Capture(() => Logger.Log("hidden", category: LogCategory.Test)).Should().BeEmpty();
	}

	private static IReadOnlyList<LogPayload> Capture(Action log)
	{
		var payloads = new List<LogPayload>();
		Action<LogPayload> handler = payloads.Add;
		Logger.OnLog += handler;

		try
		{
			log();
		}
		finally
		{
			Logger.OnLog -= handler;
		}

		return payloads;
	}
}
