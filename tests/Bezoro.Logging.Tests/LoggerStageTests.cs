using Bezoro.Logging.Types;
using FluentAssertions;

namespace Bezoro.Logging.Tests;

public sealed class LoggerStageTests
{
	[Fact]
	public void Log_WhenStageChangesAndRepeats_ShouldEmitDividerOnlyBeforeChangedStagePayload()
	{
		using var settings = new LoggerSettingsScope();
		var firstStage = $"first-{Guid.NewGuid():N}";
		var secondStage = $"second-{Guid.NewGuid():N}";
		var currentStage = firstStage;
		LoggerSettings.Stage = StageConfig.Create(() => currentStage);
		var payloads = new List<LogPayload>();
		Action<LogPayload> handler = payloads.Add;
		Logger.OnLog += handler;

		try
		{
			Logger.Log("first");
			currentStage = secondStage;
			Logger.Log("second");
			Logger.Log("repeated");
		}
		finally
		{
			Logger.OnLog -= handler;
		}

		payloads.Should().HaveCount(4);
		payloads[0].Level.Should().Be(LogLevel.Info);
		payloads[0].Message.Should().Be("first");
		payloads[0].Stage.Should().Be(firstStage);

		payloads[1].Level.Should().Be(LogLevel.Divider);
		payloads[1].Message.Should().Be($"[{firstStage}] ==============> [{secondStage}]");
		payloads[1].Stage.Should().Be(secondStage);

		payloads[2].Level.Should().Be(LogLevel.Info);
		payloads[2].Message.Should().Be("second");
		payloads[2].Stage.Should().Be(secondStage);

		payloads[3].Level.Should().Be(LogLevel.Info);
		payloads[3].Message.Should().Be("repeated");
		payloads[3].Stage.Should().Be(secondStage);
	}
}
