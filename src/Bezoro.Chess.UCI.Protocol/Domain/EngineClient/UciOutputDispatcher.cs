using Bezoro.Chess.UCI.Protocol.Domain.Common.Constants;
using Bezoro.Chess.UCI.Protocol.Domain.Common.Helpers;

namespace Bezoro.Chess.UCI.Protocol.Domain.EngineClient;

/// <summary>
///     Routes engine output lines to search coordination and waiter registries.
/// </summary>
internal sealed class UciOutputDispatcher(
	UciLineWaiterRegistry waiters,
	UciSearchCoordinator searchCoordinator,
	Action<UciProtocolMessage>? messageObserver = null)
{
	private readonly UciSearchCoordinator _searchCoordinator =
		searchCoordinator ?? throw new ArgumentNullException(nameof(searchCoordinator));
	private readonly Action<UciProtocolMessage>? _messageObserver = messageObserver;

	public void OnShutdown()
	{
		waiters.CancelAll();
		_searchCoordinator.HandleTransportTerminated();
	}

	public void Process(string line)
	{
		if (UciProtocolParser.TryParse(line, out var message))
		{
			_messageObserver?.Invoke(message!);

			if (message is UciInfoMessage { Payload.PrincipalVariation: { } pv })
				_searchCoordinator.HandleInfoLine(line, pv);
			else if (message is UciBestMoveMessage)
				_searchCoordinator.HandleBestMoveLine(line);
		}
		else
		{
			if (line.StartsWith($"{UciConstants.Prefixes.INFO} ", StringComparison.OrdinalIgnoreCase))
				_searchCoordinator.HandleInfoLine(line);
			else if (line.StartsWith($"{UciConstants.Prefixes.BEST_MOVE} ", StringComparison.OrdinalIgnoreCase))
				_searchCoordinator.HandleBestMoveLine(line);
		}

		waiters.Notify(line);
	}
}
