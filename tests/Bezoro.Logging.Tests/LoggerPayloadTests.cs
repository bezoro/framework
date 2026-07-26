using Bezoro.Logging.Types;
using FluentAssertions;

namespace Bezoro.Logging.Tests;

public sealed class LoggerPayloadTests
{
	[Fact]
	public void Log_WhenMetadataIsDisabled_ShouldDispatchCompletePayload()
	{
		using var settings = new LoggerSettingsScope();
		var payloads = new List<LogPayload>();
		var context = new object();
		Action<LogPayload> handler = payloads.Add;
		Logger.OnLog += handler;

		try
		{
			Logger.Log("hello", LogLevel.Info, LogCategory.Test, context);
		}
		finally
		{
			Logger.OnLog -= handler;
		}

		payloads.Should().ContainSingle();
		var payload = payloads[0];
		payload.Message.Should().Be("hello");
		payload.Level.Should().Be(LogLevel.Info);
		payload.Category.Should().Be(LogCategory.Test);
		payload.ContextObject.Should().BeSameAs(context);
		payload.SeverityEmoji.Should().Be(LogLevelEmoji.GetEmoji(LogLevel.Info));
		payload.CategoryEmoji.Should().Be(LogCategoryEmoji.GetEmoji(LogCategory.Test));
		payload.Style.Should().BeSameAs(LoggerSettings.InfoStyle);
		payload.FormattedMessage.Should().Be("ℹ️ [🧪] hello");
	}

	[Fact]
	public void Log_WhenFormattableStringContainsCollection_ShouldExpandCollectionInPayload()
	{
		using var settings = new LoggerSettingsScope();
		var payloads = new List<LogPayload>();
		FormattableString message = $"items: {new object?[] { "one", 2, null }}";
		Action<LogPayload> handler = payloads.Add;
		Logger.OnLog += handler;

		try
		{
			Logger.Log(message);
		}
		finally
		{
			Logger.OnLog -= handler;
		}

		payloads.Should().ContainSingle();
		payloads[0].Message.Should().Be("items: \n[one\n2\nnull]");
		payloads[0].FormattedMessage.Should().Be("ℹ️ items: \n[one\n2\nnull]");
	}

	[Fact]
	public void Log_WhenCallerAndFileMetadataAreRequested_ShouldUseExplicitCallerArguments()
	{
		using var settings = new LoggerSettingsScope();
		LoggerSettings.FileLocation = FileLocationConfig.FullPath;
		var filePath = Path.Combine("source", "Widget.cs");
		var payloads = new List<LogPayload>();
		Action<LogPayload> handler = payloads.Add;
		Logger.OnLog += handler;

		try
		{
			Logger.Log("caller", captureCallerInfo: true, memberName: "Handle", filePath: filePath);
		}
		finally
		{
			Logger.OnLog -= handler;
		}

		payloads.Should().ContainSingle();
		payloads[0].CallerInfo.Should().Be("Widget.Handle()");
		payloads[0].FormattedMessage.Should().Be($"ℹ️ caller\n  └─ {filePath} :: Widget.Handle()");
	}
}
