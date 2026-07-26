namespace Bezoro.Logging.Internal;

internal readonly record struct LogEventContext(
	DateTime Timestamp,
	long SequenceNumber,
	int ThreadId,
	int? FrameCount,
	IReadOnlyList<string>? AsyncHierarchy,
	string? Stage);
