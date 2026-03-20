using Bezoro.UCI.Domain.Common.Constants;

namespace Bezoro.UCI.Domain.EngineClient;

/// <summary>
///     Routes engine output lines to search coordination and waiter registries.
/// </summary>
internal sealed class UciOutputDispatcher(
	UciLineWaiterRegistry waiters,
	UciSearchCoordinator searchCoordinator,
	Action<string>?       lineObserver = null)
{
	private readonly UciSearchCoordinator _searchCoordinator =
		searchCoordinator ?? throw new ArgumentNullException(nameof(searchCoordinator));
	private readonly Action<string>? _lineObserver = lineObserver;

	public void OnShutdown()
	{
		waiters.CancelAll();
		_searchCoordinator.HandleTransportTerminated();
	}

	public void Process(string line)
	{
		_lineObserver?.Invoke(line);

		if (line.StartsWith($"{UciConstants.Prefixes.INFO} ", StringComparison.OrdinalIgnoreCase))
			_searchCoordinator.HandleInfoLine(line);
		else if (line.StartsWith($"{UciConstants.Prefixes.BEST_MOVE} ", StringComparison.OrdinalIgnoreCase))
			_searchCoordinator.HandleBestMoveLine(line);

		waiters.Notify(line);
	}
}
