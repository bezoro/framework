using Bezoro.Logging.Types;
using FluentAssertions;

namespace Bezoro.Logging.Tests;

public sealed class LoggerExceptionTests
{
	[Fact]
	public void LogException_WhenExceptionHasInnerException_ShouldRetainExtractedDetails()
	{
		using var settings = new LoggerSettingsScope();
		var exception = CaptureException();
		var payloads = new List<LogPayload>();
		Action<LogPayload> handler = payloads.Add;
		Logger.OnLog += handler;

		try
		{
			Logger.LogException(
				exception,
				"while working",
				LogCategory.Test,
				captureCallerInfo: true,
				memberName: "Run",
				filePath: @"C:\source\Worker.cs");
		}
		finally
		{
			Logger.OnLog -= handler;
		}

		payloads.Should().ContainSingle();
		var payload = payloads[0];
		payload.Exception.Should().BeSameAs(exception);
		payload.Message.Should().Be("while working | outer failure");
		payload.ExceptionType.Should().Be(nameof(InvalidOperationException));
		payload.StackTrace.Should().Be(exception.StackTrace).And.NotBeNullOrWhiteSpace();
		payload.InnerExceptionType.Should().Be(nameof(ArgumentException));
		payload.InnerExceptionMessage.Should().Be("inner failure");
		payload.CallerInfo.Should().Be("Worker.Run()");
		payload.FormattedMessage.Should().Be(
			"🆘 [🧪] InvalidOperationException :: while working | outer failure\n  └─ Worker.Run()");
	}

	private static Exception CaptureException()
	{
		try
		{
			throw new InvalidOperationException("outer failure", new ArgumentException("inner failure"));
		}
		catch (Exception exception)
		{
			return exception;
		}
	}
}
