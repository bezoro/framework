using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.UCI.API.Types;

namespace Bezoro.UCI.Domain;

internal sealed class PonderEngine : IAsyncDisposable, IDisposable
{
	private readonly UciEngineClient _client;

	private int? _lastScoreCp;
	private int? _lastScoreMate;

	public event Action<ParsedMove, ParsedMove?>? BestMove;
	public event Action<PrincipalVariation>?      InfoPv;

	public PonderEngine(string enginePath, IEnumerable<string>? args = null, string? workingDirectory = null)
	{
		var transport = new ProcessUciTransport(enginePath, args, workingDirectory);
		_client                =  new(transport);
		_client.InfoPvReceived += OnClientInfoPvReceived;
	}

	public bool IsHealthy => _client.IsHealthy;
	public bool IsStarted => _client.IsStarted;

	public EngineActivity                      Activity => _client.Activity;
	public ProcessUciTransport.TransportStatus Status   => _client.Status;

	public async Task NewGameAsync(CancellationToken ct = default)
	{
		await _client.StopSearchAsync(ct).ConfigureAwait(false);
		await _client.UciNewGameAsync(ct).ConfigureAwait(false);

		ClearLastScores();
	}

	/// <summary>
	///     Forwards option setting to the underlying engine client.
	/// </summary>
	public Task SetOptionAsync(string name, string? value, CancellationToken ct = default) =>
		_client.SetOptionAsync(name, value, ct);

	/// <summary>
	///     Sets the engine position without starting a search. Keeps ponder engine state synchronized with other engines.
	/// </summary>
	public Task SetPositionAsync(Fen fen, IEnumerable<string>? moves = null, CancellationToken ct = default) =>
		_client.SetPositionAsync(fen, moves, ct);

	public async Task StartAsync(CancellationToken ct = default)
	{
		await _client.StartAsync(ct).ConfigureAwait(false);
		ClearLastScores();
	}

	/// <summary>
	///     Starts an infinite search on the client. InfoPv is forwarded for each PV update and
	///     BestMove is raised when the PV improves (mate over cp; higher is better).
	/// </summary>
	public async Task StartSearchAsync(
		Fen                  fen,
		IEnumerable<string>? playedMoves,
		CancellationToken    ct = default)
	{
		if (Activity is EngineActivity.Searching or EngineActivity.Pondering) return;

		// Reset last scores when starting a new search to synchronize internal evaluation state
		ClearLastScores();

		await _client.SetPositionAsync(fen, playedMoves, ct).ConfigureAwait(false);
		await _client.GoFireAndForgetAsync(new() { Infinite = true }, ct).ConfigureAwait(false);
	}

	public async Task StopAsync(CancellationToken ct = default)
	{
		await _client.StopAsync(ct).ConfigureAwait(false);
		ClearLastScores();
	}

	/// <summary>
	///     Stops any ongoing search (best or ponder).
	/// </summary>
	public Task StopSearchAsync(CancellationToken ct = default)
	{
		_lastScoreMate = null;
		_lastScoreCp   = null;
		return _client.StopSearchAsync(ct);
	}

	public ValueTask DisposeAsync() => _client.DisposeAsync();

	public void Dispose()
	{
		DisposeAsync().AsTask().GetAwaiter().GetResult();
	}

	private void ClearLastScores()
	{
		_lastScoreMate = null;
		_lastScoreCp   = null;
	}

	private void OnClientInfoPvReceived(PrincipalVariation pv)
	{
		InfoPv?.Invoke(pv);

		var  improved = false;
		int? newMate  = pv.ScoreMate;
		int? newCp    = pv.ScoreCp;

		if (newMate.HasValue)
		{
			improved = !_lastScoreMate.HasValue || newMate.Value < _lastScoreMate.Value;
			if (improved)
				_lastScoreMate = newMate;
		}
		else
		{
			if (!_lastScoreMate.HasValue &&
				newCp.HasValue &&
				(!_lastScoreCp.HasValue || newCp.Value > _lastScoreCp.Value))
			{
				_lastScoreCp = newCp;
				improved     = true;
			}
		}

		if (!improved) return;

		string bestStr = pv.Moves is { Count: > 0 } ? pv.Moves[0] : string.Empty;
		if (string.IsNullOrWhiteSpace(bestStr)) return;

		var bestParsed = ParsedMove.FromNotation(bestStr);

		ParsedMove? ponderParsed = null;
		if (pv.Moves is { Count: > 1 })
		{
			string ponderStr = pv.Moves[1];
			if (!string.IsNullOrWhiteSpace(ponderStr))
				ponderParsed = ParsedMove.FromNotation(ponderStr);
		}

		BestMove?.Invoke(bestParsed, ponderParsed);
	}
}
