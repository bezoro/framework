using Bezoro.Logging.Types;

namespace Bezoro.Logging.Internal;

internal readonly record struct LogSettingsSnapshot(
	FileLocationConfig FileLocation,
	FrameCountConfig FrameCount,
	GroupingConfig Grouping,
	SequenceNumberConfig SequenceNumber,
	ThreadIdConfig ThreadId,
	TimestampConfig Timestamp,
	LogStyle ErrorStyle,
	LogStyle ExceptionStyle,
	LogStyle InfoStyle,
	LogStyle SuccessStyle,
	LogStyle WarningStyle)
{
	internal static LogSettingsSnapshot Capture() => new(
		LoggerSettings.FileLocation,
		LoggerSettings.FrameCount,
		LoggerSettings.Grouping,
		LoggerSettings.SequenceNumber,
		LoggerSettings.ThreadId,
		LoggerSettings.Timestamp,
		LoggerSettings.ErrorStyle,
		LoggerSettings.ExceptionStyle,
		LoggerSettings.InfoStyle,
		LoggerSettings.SuccessStyle,
		LoggerSettings.WarningStyle);

	internal LogStyle GetStyle(LogLevel level) => level switch
	{
		LogLevel.Info      => InfoStyle,
		LogLevel.Success   => SuccessStyle,
		LogLevel.Warning   => WarningStyle,
		LogLevel.Error     => ErrorStyle,
		LogLevel.Exception => ExceptionStyle,
		_                  => InfoStyle
	};
}
