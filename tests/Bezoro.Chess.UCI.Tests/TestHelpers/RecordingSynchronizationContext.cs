namespace Bezoro.Chess.UCI.Tests.TestHelpers;

public sealed class RecordingSynchronizationContext : SynchronizationContext
{
	private int _postCount;

	public int PostCount => Volatile.Read(ref _postCount);

	public override void Post(SendOrPostCallback d, object? state)
	{
		Interlocked.Increment(ref _postCount);

		var previous = Current;
		try
		{
			SetSynchronizationContext(this);
			d(state);
		}
		finally
		{
			SetSynchronizationContext(previous);
		}
	}
}
