using Bezoro.Logging.Types;

namespace Bezoro.Logging.Internal;

internal readonly record struct LogInput(
	string Message,
	LogLevel Level,
	LogCategory? Category,
	object? ContextObject,
	Exception? Exception,
	string? ExceptionType,
	string? CallerInfo,
	string? StackTrace,
	string? InnerExceptionType,
	string? InnerExceptionMessage,
	string? FilePath);
