using Bezoro.Logging.Types;
using FluentAssertions;

namespace Bezoro.Logging.Tests;

public sealed class LoggerSettingsSnapshotTests
{
	[Fact]
	public void Log_WhenFrameCountIsEnabled_ShouldInvokeProviderOnceAndIncludeFrame()
	{
		using var settings = new LoggerSettingsScope();
		var providerCalls = 0;
		LoggerSettings.FrameCount = FrameCountConfig.Create(() =>
		{
			providerCalls++;
			return 42;
		});
		LogPayload? payload = null;
		Action<LogPayload> handler = value => payload = value;
		Logger.OnLog += handler;

		try
		{
			Logger.Log("framed");
		}
		finally
		{
			Logger.OnLog -= handler;
		}

		providerCalls.Should().Be(1);
		payload.Should().NotBeNull();
		payload!.FormattedMessage.Should().Be("[F42] ℹ️ framed");
	}

	[Fact]
	public void Log_WhenFiltered_ShouldNotInvokeFrameOrStageProviders()
	{
		using var settings = new LoggerSettingsScope();
		var frameProviderCalls = 0;
		var stageProviderCalls = 0;
		Logger.MinimumLevel = LogLevel.Error;
		LoggerSettings.FrameCount = FrameCountConfig.Create(() =>
		{
			frameProviderCalls++;
			return 42;
		});
		LoggerSettings.Stage = StageConfig.Create(() =>
		{
			stageProviderCalls++;
			return "filtered";
		});
		var payloads = new List<LogPayload>();
		Action<LogPayload> handler = payloads.Add;
		Logger.OnLog += handler;

		try
		{
			Logger.Log("hidden");
		}
		finally
		{
			Logger.OnLog -= handler;
		}

		frameProviderCalls.Should().Be(0);
		stageProviderCalls.Should().Be(0);
		payloads.Should().BeEmpty();
	}

	[Fact]
	public void Log_WhenFrameProviderMutatesStyle_ShouldUseCapturedStyle()
	{
		using var settings = new LoggerSettingsScope();
		var capturedStyle = new LogStyle(ConsoleColor.Cyan);
		LoggerSettings.InfoStyle = capturedStyle;
		LoggerSettings.FrameCount = FrameCountConfig.Create(() =>
		{
			LoggerSettings.InfoStyle = new LogStyle(ConsoleColor.Magenta);
			return 42;
		});
		LogPayload? payload = null;
		Action<LogPayload> handler = value => payload = value;
		Logger.OnLog += handler;

		try
		{
			Logger.Log("snapshot");
		}
		finally
		{
			Logger.OnLog -= handler;
		}

		payload.Should().NotBeNull();
		payload!.Style.Should().BeSameAs(capturedStyle);
	}

	[Fact]
	public void Log_WhenEarlierSubscriberMutatesSettings_ShouldKeepBuiltPayloadUnchanged()
	{
		using var settings = new LoggerSettingsScope();
		var capturedStyle = new LogStyle(ConsoleColor.Cyan);
		LoggerSettings.InfoStyle = capturedStyle;
		Action<LogPayload> mutatingHandler = _ => LoggerSettings.InfoStyle = new LogStyle(ConsoleColor.Magenta);
		LogPayload? observedPayload = null;
		Action<LogPayload> observingHandler = value => observedPayload = value;
		Logger.OnLog += mutatingHandler;
		Logger.OnLog += observingHandler;

		try
		{
			Logger.Log("built");
		}
		finally
		{
			Logger.OnLog -= mutatingHandler;
			Logger.OnLog -= observingHandler;
		}

		observedPayload.Should().NotBeNull();
		observedPayload!.Style.Should().BeSameAs(capturedStyle);
		observedPayload.FormattedMessage.Should().Be("ℹ️ built");
	}

	[Fact]
	public void Log_WhenSubscriberIsRegistered_ShouldInvokeSynchronouslyOnCallerThread()
	{
		using var settings = new LoggerSettingsScope();
		var callerThreadId = Environment.CurrentManagedThreadId;
		int? subscriberThreadId = null;
		var subscriberCompleted = false;
		Action<LogPayload> handler = _ =>
		{
			subscriberThreadId = Environment.CurrentManagedThreadId;
			subscriberCompleted = true;
		};
		Logger.OnLog += handler;

		try
		{
			Logger.Log("synchronous");
			subscriberCompleted.Should().BeTrue();
		}
		finally
		{
			Logger.OnLog -= handler;
		}

		subscriberThreadId.Should().Be(callerThreadId);
	}
}
