using Bezoro.Logging.Internal;
using Bezoro.Logging.Types;
using FluentAssertions;

namespace Bezoro.Logging.Tests;

public sealed class LogPayloadFormatterTests
{
	[Fact]
	public void Format_WhenAllMetadataIsEnabled_ShouldBuildCompletePayload()
	{
		var contextObject = new object();
		var style = new LogStyle(ConsoleColor.Yellow, true);
		var timestamp = new DateTime(2025, 6, 7, 8, 9, 10, DateTimeKind.Utc);
		var filePath = Path.Combine("source", "Widget.cs");
		string[] hierarchy = ["root", "child"];
		var input = new LogInput(
			"ready",
			LogLevel.Warning,
			LogCategory.Test,
			contextObject,
			null,
			null,
			"Widget.Run()",
			null,
			null,
			null,
			filePath);
		var settings = CreateSettings(
			FileLocationConfig.FullPath,
			FrameCountConfig.Create(() => throw new InvalidOperationException("formatter invoked provider")),
			GroupingConfig.Create(LoggerSettings.ContextGrouping.CallerType),
			SequenceNumberConfig.On,
			ThreadIdConfig.On,
			TimestampConfig.Create("yyyy-MM-dd HH:mm:ss"),
			warningStyle: style);
		var context = new LogEventContext(timestamp, 7, 11, 42, hierarchy, "Play");

		var payload = LogPayloadFormatter.Format(input, settings, context);

		payload.Timestamp.Should().Be(timestamp);
		payload.Level.Should().Be(LogLevel.Warning);
		payload.Category.Should().Be(LogCategory.Test);
		payload.Message.Should().Be("ready");
		payload.SeverityEmoji.Should().Be("⚠️");
		payload.CategoryEmoji.Should().Be("🧪");
		payload.Exception.Should().BeNull();
		payload.ExceptionType.Should().BeNull();
		payload.CallerInfo.Should().Be("Widget.Run()");
		payload.Stage.Should().Be("Play");
		payload.ContextObject.Should().BeSameAs(contextObject);
		payload.StackTrace.Should().BeNull();
		payload.InnerExceptionType.Should().BeNull();
		payload.InnerExceptionMessage.Should().BeNull();
		payload.GroupingContext.Should().Be("Widget");
		payload.AsyncContextHierarchy.Should().BeSameAs(hierarchy);
		payload.AsyncContext.Should().Be("root > child");
		payload.AsyncContextDepth.Should().Be(2);
		payload.Style.Should().BeSameAs(style);
		payload.FormattedMessage.Should().Be(
			$"🔄 [root > child]\n[#7 2025-06-07 08:09:10 F42 T11 G:Widget] ⚠️ [🧪] ready\n  └─ {filePath} :: Widget.Run()");
	}

	[Fact]
	public void Format_WhenInputContainsException_ShouldRetainExceptionFields()
	{
		var innerException = new ArgumentException("inner");
		var exception = new InvalidOperationException("outer", innerException);
		var style = new LogStyle(ConsoleColor.DarkRed, true);
		var input = new LogInput(
			"failed",
			LogLevel.Exception,
			null,
			null,
			exception,
			nameof(InvalidOperationException),
			null,
			"trace",
			nameof(ArgumentException),
			"inner",
			null);
		var settings = CreateSettings(exceptionStyle: style);
		var context = new LogEventContext(default, 0, 0, null, null, null);

		var payload = LogPayloadFormatter.Format(input, settings, context);

		payload.Exception.Should().BeSameAs(exception);
		payload.ExceptionType.Should().Be(nameof(InvalidOperationException));
		payload.StackTrace.Should().Be("trace");
		payload.InnerExceptionType.Should().Be(nameof(ArgumentException));
		payload.InnerExceptionMessage.Should().Be("inner");
		payload.Style.Should().BeSameAs(style);
		payload.FormattedMessage.Should().Be("🆘 InvalidOperationException :: failed");
	}

	[Fact]
	public void Format_WhenMetadataIsDisabled_ShouldBuildSimpleMainLine()
	{
		var input = new LogInput("simple", LogLevel.Info, null, null, null, null, null, null, null, null, null);
		var settings = CreateSettings();
		var context = new LogEventContext(default, 0, 0, null, null, null);

		var payload = LogPayloadFormatter.Format(input, settings, context);

		payload.FormattedMessage.Should().Be("ℹ️ simple");
		payload.GroupingContext.Should().BeNull();
		payload.AsyncContext.Should().BeNull();
		payload.AsyncContextDepth.Should().Be(0);
	}

	[Fact]
	public void FormatStageDivider_WhenContextIsSupplied_ShouldBuildCompleteDividerPayload()
	{
		var contextObject = new object();
		var exception = new InvalidOperationException("ignored");
		var style = new LogStyle(ConsoleColor.Red, true);
		var timestamp = new DateTime(2025, 7, 8, 9, 10, 11, DateTimeKind.Utc);
		string[] hierarchy = ["root", "worker"];
		var input = new LogInput(
			"ignored",
			LogLevel.Error,
			LogCategory.Gameplay,
			contextObject,
			exception,
			nameof(InvalidOperationException),
			"Widget.Run()",
			"trace",
			nameof(ArgumentException),
			"inner",
			"Widget.cs");
		var settings = CreateSettings(
			frameCount: FrameCountConfig.Create(() => throw new InvalidOperationException("formatter invoked provider")),
			errorStyle: style);
		var context = new LogEventContext(timestamp, 99, 42, 7, hierarchy, "Play");

		var payload = LogPayloadFormatter.FormatStageDivider("--- Play ---", input, settings, context);

		payload.Timestamp.Should().Be(timestamp);
		payload.Level.Should().Be(LogLevel.Divider);
		payload.Category.Should().Be(LogCategory.None);
		payload.Message.Should().Be("--- Play ---");
		payload.FormattedMessage.Should().Be("--- Play ---");
		payload.SeverityEmoji.Should().Be("❌");
		payload.CategoryEmoji.Should().Be("🎮");
		payload.Style.Should().BeSameAs(style);
		payload.ContextObject.Should().BeSameAs(contextObject);
		payload.Exception.Should().BeNull();
		payload.ExceptionType.Should().BeNull();
		payload.CallerInfo.Should().BeNull();
		payload.StackTrace.Should().BeNull();
		payload.InnerExceptionType.Should().BeNull();
		payload.InnerExceptionMessage.Should().BeNull();
		payload.GroupingContext.Should().BeNull();
		payload.Stage.Should().Be("Play");
		payload.AsyncContextHierarchy.Should().BeSameAs(hierarchy);
		payload.AsyncContext.Should().Be("root > worker");
		payload.AsyncContextDepth.Should().Be(2);
	}

	private static LogSettingsSnapshot CreateSettings(
		FileLocationConfig? fileLocation = null,
		FrameCountConfig? frameCount = null,
		GroupingConfig? grouping = null,
		SequenceNumberConfig? sequenceNumber = null,
		ThreadIdConfig? threadId = null,
		TimestampConfig? timestamp = null,
		LogStyle? errorStyle = null,
		LogStyle? exceptionStyle = null,
		LogStyle? infoStyle = null,
		LogStyle? successStyle = null,
		LogStyle? warningStyle = null) =>
		new(
			fileLocation ?? FileLocationConfig.Disabled,
			frameCount ?? FrameCountConfig.Disabled,
			grouping ?? GroupingConfig.None,
			sequenceNumber ?? SequenceNumberConfig.Off,
			threadId ?? ThreadIdConfig.Off,
			timestamp ?? TimestampConfig.Disabled,
			errorStyle ?? new LogStyle(ConsoleColor.Red),
			exceptionStyle ?? new LogStyle(ConsoleColor.DarkRed),
			infoStyle ?? new LogStyle(ConsoleColor.White),
			successStyle ?? new LogStyle(ConsoleColor.Green),
			warningStyle ?? new LogStyle(ConsoleColor.Yellow));
}
