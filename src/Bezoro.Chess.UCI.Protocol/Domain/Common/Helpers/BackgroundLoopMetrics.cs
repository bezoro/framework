using System.Threading;

namespace Bezoro.Chess.UCI.Protocol.Domain.Common.Helpers;

/// <summary>
///     Thread-safe metrics tracker for background loops.
/// </summary>
internal sealed class BackgroundLoopMetrics
{
	private long _backpressureEvents;
	private long _linesRead;
	private long _linesWritten;

	public long BackpressureEvents => Interlocked.Read(ref _backpressureEvents);
	public long LinesRead          => Interlocked.Read(ref _linesRead);
	public long LinesWritten       => Interlocked.Read(ref _linesWritten);

	public void IncrementBackpressureEvents() => Interlocked.Increment(ref _backpressureEvents);
	public void IncrementLinesRead()          => Interlocked.Increment(ref _linesRead);
	public void IncrementLinesWritten()       => Interlocked.Increment(ref _linesWritten);
}
