using Bezoro.Logging.Types;

namespace Bezoro.Logging.Tests;

internal sealed class LoggerSettingsScope : IDisposable
{
	private readonly LogLevel _minimumLevel = Logger.MinimumLevel;
	private readonly bool _enabled = LoggerSettings.Enabled;
	private readonly FileLocationConfig _fileLocation = LoggerSettings.FileLocation;
	private readonly FrameCountConfig _frameCount = LoggerSettings.FrameCount;
	private readonly GroupingConfig _grouping = LoggerSettings.Grouping;
	private readonly LogStyle _errorStyle = LoggerSettings.ErrorStyle;
	private readonly LogStyle _exceptionStyle = LoggerSettings.ExceptionStyle;
	private readonly LogStyle _infoStyle = LoggerSettings.InfoStyle;
	private readonly LogStyle _successStyle = LoggerSettings.SuccessStyle;
	private readonly LogStyle _warningStyle = LoggerSettings.WarningStyle;
	private readonly SequenceNumberConfig _sequenceNumber = LoggerSettings.SequenceNumber;
	private readonly StageConfig _stage = LoggerSettings.Stage;
	private readonly ThreadIdConfig _threadId = LoggerSettings.ThreadId;
	private readonly TimestampConfig _timestamp = LoggerSettings.Timestamp;
	private readonly LogCategory[] _mutedCategories = [.. LoggerSettings.MutedCategories];

	public LoggerSettingsScope()
	{
		Logger.MinimumLevel = LogLevel.Info;
		LoggerSettings.Enabled = true;
		LoggerSettings.FileLocation = FileLocationConfig.Disabled;
		LoggerSettings.FrameCount = FrameCountConfig.Disabled;
		LoggerSettings.Grouping = GroupingConfig.None;
		LoggerSettings.SequenceNumber = SequenceNumberConfig.Off;
		LoggerSettings.Stage = StageConfig.Disabled;
		LoggerSettings.ThreadId = ThreadIdConfig.Off;
		LoggerSettings.Timestamp = TimestampConfig.Disabled;
		LoggerSettings.MutedCategories.Clear();
	}

	public void Dispose()
	{
		Logger.MinimumLevel = _minimumLevel;
		LoggerSettings.Enabled = _enabled;
		LoggerSettings.FileLocation = _fileLocation;
		LoggerSettings.FrameCount = _frameCount;
		LoggerSettings.Grouping = _grouping;
		LoggerSettings.ErrorStyle = _errorStyle;
		LoggerSettings.ExceptionStyle = _exceptionStyle;
		LoggerSettings.InfoStyle = _infoStyle;
		LoggerSettings.SuccessStyle = _successStyle;
		LoggerSettings.WarningStyle = _warningStyle;
		LoggerSettings.SequenceNumber = _sequenceNumber;
		LoggerSettings.Stage = _stage;
		LoggerSettings.ThreadId = _threadId;
		LoggerSettings.Timestamp = _timestamp;
		LoggerSettings.MutedCategories.Clear();
		LoggerSettings.MutedCategories.UnionWith(_mutedCategories);
	}
}
