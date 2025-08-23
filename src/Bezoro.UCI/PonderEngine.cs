using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.UCI.API.Types;

namespace Bezoro.UCI;

internal sealed class PonderEngine : IAsyncDisposable, IDisposable
{
	private readonly object          _cacheLock = new();
	private readonly UciEngineClient _client;
	private          bool            _isPondering;
	private          string?         _lastPositionKey;

	public event Action<string, string>?     BestMove;
	public event Action<PrincipalVariation>? InfoPv;

	public PonderEngine(string enginePath, IEnumerable<string>? args = null, string? workingDirectory = null)
	{
		var transport = new ProcessUciTransport(enginePath, args, workingDirectory);
		_client                  =  new(transport);
		_client.InfoPvReceived   += pv => InfoPv?.Invoke(pv);
		_client.BestMoveReceived += (b, p) => BestMove?.Invoke(b, p);
	}

	public bool IsHealthy => _client.IsHealthy;
	public bool IsStarted => _client.IsStarted;

	public EngineActivity Activity => _client.Activity;

	public ProcessUciTransport.TransportStatus Status => _client.Status;

	public async Task StartAsync(CancellationToken ct = default)
	{
		await _client.StartAsync(ct).ConfigureAwait(false);
		// Clear cached state at start
		lock (_cacheLock)
		{
			_isPondering     = false;
			_lastPositionKey = null;
		}
	}

	public async Task StartPonderAsync(Fen fen, IEnumerable<string>? playedMoves, CancellationToken ct = default)
	{
		string key = BuildPositionKey(fen, playedMoves);
		lock (_cacheLock)
		{
			// If we're already pondering exactly this position, skip restarting
			if (_isPondering && string.Equals(_lastPositionKey, key, StringComparison.Ordinal)) return;

			// Update last key; mark as not pondering until commands are sent
			_lastPositionKey = key;
		}

		await _client.SetPositionAsync(fen, playedMoves, ct).ConfigureAwait(false);
		await _client.GoFireAndForgetAsync(new() { Ponder = true, Infinite = true }, ct).ConfigureAwait(false);

		lock (_cacheLock)
		{
			_isPondering = true;
		}
	}

	public async Task StopAsync(CancellationToken ct = default)
	{
		await _client.StopAsync(ct).ConfigureAwait(false);
		// Clear cached state on stop
		lock (_cacheLock)
		{
			_isPondering     = false;
			_lastPositionKey = null;
		}
	}

	public async Task StopPonderAsync(CancellationToken ct = default)
	{
		await _client.StopSearchAsync(ct).ConfigureAwait(false);
		// Clear pondering state so future identical requests are not skipped
		lock (_cacheLock)
		{
			_isPondering     = false;
			_lastPositionKey = null;
		}
	}

	public ValueTask DisposeAsync() => _client.DisposeAsync();

	public void Dispose()
	{
		DisposeAsync().AsTask().GetAwaiter().GetResult();
	}

	private static string BuildPositionKey(Fen fen, IEnumerable<string>? playedMoves)
	{
		string movesJoined = playedMoves is null ? string.Empty : string.Join(' ', playedMoves);
		return $"{fen}|{movesJoined}";
	}
}
