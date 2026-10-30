using Bezoro.Logging.Types;
using FluentAssertions;

namespace Bezoro.Logging.Tests;

public sealed class LoggerFilteringTests
{
	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(2)]
	public void FilteredLog_DoesNotFormatOrEnumerateTheMessage(int filter)
	{
		using var settings = new LoggerSettingsScope();
		if (filter == 0) LoggerSettings.Enabled = false;
		if (filter == 1) Logger.MinimumLevel = LogLevel.Error;
		if (filter == 2) LoggerSettings.MutedCategories.Add(LogCategory.Test);

		Capture(() => Logger.Log(new UnformattableMessage(), category: LogCategory.Test)).Should().BeEmpty();
	}

	private sealed class UnformattableMessage
	{
		public override string ToString() => throw new InvalidOperationException("Filtered messages must not be formatted.");
	}

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
