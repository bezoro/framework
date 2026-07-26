using Bezoro.Logging.Types;

namespace Bezoro.Logging.Internal;

internal sealed class AsyncContextScope : IDisposable
{
	private readonly AsyncContextNode? _previous;
	private int _disposed;

	internal AsyncContextScope(string contextName)
	{
		_previous = LoggerSettings.GetCurrentAsyncContext();
		LoggerSettings.SetCurrentAsyncContext(new(contextName, _previous));
	}

	public void Dispose()
	{
		if (Interlocked.Exchange(ref _disposed, 1) == 0)
			LoggerSettings.SetCurrentAsyncContext(_previous);
	}
}
