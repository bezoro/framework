using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.UCI.API.Types;

namespace Bezoro.UCI.Domain;

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

	public EngineActivity                      Activity => _client.Activity;
	public ProcessUciTransport.TransportStatus Status   => _client.Status;

	public async Task NewGameAsync(CancellationToken ct = default)
	{
		// Ensure any ongoing search is stopped so 'isready' can return promptly.
		try
		{
			await _client.StopSearchAsync(ct).ConfigureAwait(false);
		}
		catch
		{
			/* best-effort */
		}

		await _client.UciNewGameAsync(ct).ConfigureAwait(false);

		// Reset cached pondering state after a new game starts
		lock (_cacheLock)
		{
			_isPondering     = false;
			_lastPositionKey = null;
		}
	}

	/// <summary>
	///     Forwards option setting to the underlying engine client.
	/// </summary>
	public Task SetOptionAsync(string name, string? value, CancellationToken ct = default) =>
		_client.SetOptionAsync(name, value, ct);

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

			_lastPositionKey = key;
		}

		await StartSearchAsync(fen, playedMoves, true, ct).ConfigureAwait(false);

		lock (_cacheLock)
		{
			_isPondering = true;
		}
	}

	/// <summary>
	///     Starts an infinite search; when 'ponder' is true the engine runs in pondering mode.
	///     Subscribers consume InfoPv and BestMove events to derive best/ponder updates.
	/// </summary>
	public async Task StartSearchAsync(
		Fen                  fen,
		IEnumerable<string>? playedMoves,
		bool                 ponder,
		CancellationToken    ct = default)
	{
		await _client.SetPositionAsync(fen, playedMoves, ct).ConfigureAwait(false);
		await _client.GoFireAndForgetAsync(new() { Ponder = ponder, Infinite = true }, ct).ConfigureAwait(false);
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

	/// <summary>
	///     Stops any ongoing search (best or ponder).
	/// </summary>
	public Task StopSearchAsync(CancellationToken ct = default) => _client.StopSearchAsync(ct);

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
