using System.Threading;

namespace Bezoro.Chess.UCI.Internal;

internal sealed class GameEngineEventDispatcher(SynchronizationContext? syncContext)
{
	private readonly SynchronizationContext? _syncContext = syncContext;

	public void Raise(Action? handler)
	{
		if (handler is null)
			return;

		if (_syncContext is { })
			_syncContext.Post(_ => handler(), null);
		else
			handler();
	}

	public void Raise<T>(Action<T>? handler, T args)
	{
		if (handler is null)
			return;

		if (_syncContext is { })
			_syncContext.Post(_ => handler(args), null);
		else
			handler(args);
	}
}
