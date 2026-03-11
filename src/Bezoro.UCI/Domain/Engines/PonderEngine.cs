using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.UCI.API.Types;

namespace Bezoro.UCI.Domain.Engines;

internal sealed class PonderEngine : IAsyncDisposable, IDisposable
{
	private readonly object          _scoreLock = new();
	private readonly UciEngineClient _client;
	private readonly Func<CancellationToken, Task>? _stopHook;

	private int? _lastScoreCp;
	private int? _lastScoreMate;
	private int  _searchActive;
	private int  _forwardingGeneration;
	private int  _searchGeneration;

	public event Action<ParsedMove, ParsedMove?>? BestMove;
	public event Action<PrincipalVariation>?      InfoPv;
	internal event Action<ParsedMove, ParsedMove?, int>? BestMoveWithGeneration;
	internal event Action<PrincipalVariation, int>?      InfoPvWithGeneration;

	public PonderEngine(string enginePath, IEnumerable<string>? args = null, string? workingDirectory = null)
		: this(enginePath, args, workingDirectory, null) { }

	internal PonderEngine(
		string                          enginePath,
		IEnumerable<string>?            args,
		string?                         workingDirectory,
		Func<CancellationToken, Task>?  stopHook)
	{
		var transport = new ProcessUciTransport(enginePath, args, workingDirectory);
		_client                = new(transport);
		_stopHook              = stopHook;
		_client.InfoPvReceived += OnClientInfoPvReceived;
	}

	public bool IsHealthy => _client.IsHealthy;
	public bool IsStarted => _client.IsStarted;
	internal int CurrentSearchGeneration => Volatile.Read(ref _searchGeneration);

	public EngineActivity  Activity => _client.Activity;
	public TransportStatus Status   => _client.Status;

	public async Task NewGameAsync(CancellationToken ct = default)
	{
		await StopSearchAsync(ct).ConfigureAwait(false);
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
		if (Volatile.Read(ref _searchActive) == 1)
			await StopSearchAsync(ct).ConfigureAwait(false);

		// Reset last scores when starting a new search to synchronize internal evaluation state
		ClearLastScores();
		DisableOutputForwarding();
		int generation = Interlocked.Increment(ref _searchGeneration);

		try
		{
			await _client.SetPositionAsync(fen, playedMoves, ct).ConfigureAwait(false);
			EnableOutputForwarding(generation);
			await _client.GoFireAndForgetAsync(new() { Infinite = true }, ct).ConfigureAwait(false);
			Volatile.Write(ref _searchActive, 1);
		}
		catch
		{
			DisableOutputForwarding();
			Volatile.Write(ref _searchActive, 0);
			Interlocked.CompareExchange(ref _searchGeneration, generation + 1, generation);
			throw;
		}
	}

	public async Task StopAsync(CancellationToken ct = default)
	{
		await StopSearchAsync(ct).ConfigureAwait(false);
		await _client.StopAsync(ct).ConfigureAwait(false);
		ClearLastScores();
		if (_stopHook is { })
			await _stopHook(ct).ConfigureAwait(false);
	}

	/// <summary>
	///     Stops any ongoing search (best or ponder).
	/// </summary>
	public async Task StopSearchAsync(CancellationToken ct = default)
	{
		DisableOutputForwarding();
		Volatile.Write(ref _searchActive, 0);
		Interlocked.Increment(ref _searchGeneration);

		lock (_scoreLock)
		{
			_lastScoreMate = null;
			_lastScoreCp   = null;
		}

		if (!_client.IsStarted) return;

		await _client.StopSearchAsync(ct).ConfigureAwait(false);
		await _client.IsReadyAsync(ct).ConfigureAwait(false);
	}

	public ValueTask DisposeAsync() => _client.DisposeAsync();

	public void Dispose()
	{
		DisposeAsync().AsTask().GetAwaiter().GetResult();
	}

	internal void EnableOutputForwardingForTests(int generation = 1)
	{
		Volatile.Write(ref _searchGeneration, generation);
		EnableOutputForwarding(generation);
	}

	/// <summary>
	///     Determines if the new score represents an improvement over the last score.
	///     For mate scores: positive = winning (lower is better), negative = losing (higher/less negative is better).
	///     For cp scores: higher is better.
	///     Mate scores are always preferred over cp scores when both are winning or both are losing.
	/// </summary>
	internal static bool IsScoreImproved(int? newMate, int? newCp, int? lastMate, int? lastCp)
	{
		if (newMate.HasValue)
		{
			// New score is a mate score
			if (lastMate.HasValue)
			{
				// Both are mate scores: compare correctly based on sign
				// Positive (winning): lower is better (mate in 1 < mate in 5)
				// Negative (losing): higher/less negative is better (mate in -1 > mate in -5)
				if (newMate.Value > 0 && lastMate.Value > 0)
					return newMate.Value < lastMate.Value; // Both winning: lower is better

				if (newMate.Value < 0 && lastMate.Value < 0)
					return newMate.Value > lastMate.Value; // Both losing: higher (less negative) is better

				// Different signs: positive is always better
				return newMate.Value > lastMate.Value;
			}

			// Transition from cp to mate: mate is always better than cp
			return true;
		}

		// New score is a cp score
		if (!newCp.HasValue) return false; // No valid score

		if (lastMate.HasValue)
		{
			// Transition from mate to cp
			// Positive mate (winning) is always better than any cp
			if (lastMate.Value > 0) return false;

			// Negative mate (losing) vs cp:
			// - If cp is positive (winning), it's better than losing mate
			// - If cp is negative (losing), compare: higher cp is better
			if (newCp.Value > 0) return true; // Winning cp is better than losing mate

			// Both losing: compare cp values (higher is better)
			// Note: We can't directly compare mate distance to cp, but if we're transitioning
			// from mate to cp and both are losing, we prefer the higher cp score
			return !lastCp.HasValue || newCp.Value > lastCp.Value;
		}

		// Both are cp scores: higher is better
		return !lastCp.HasValue || newCp.Value > lastCp.Value;
	}

	internal void OnClientInfoPvReceived(PrincipalVariation pv)
	{
		int generation = Volatile.Read(ref _forwardingGeneration);
		if (generation == 0) return;

		InfoPvWithGeneration?.Invoke(pv, generation);
		InfoPv?.Invoke(pv);

		int? newMate = pv.ScoreMate;
		int? newCp   = pv.ScoreCp;

		bool improved;
		lock (_scoreLock)
		{
			improved = IsScoreImproved(newMate, newCp, _lastScoreMate, _lastScoreCp);

			if (improved)
			{
				_lastScoreMate = newMate;
				_lastScoreCp   = newCp;
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

		BestMoveWithGeneration?.Invoke(bestParsed, ponderParsed, generation);
		BestMove?.Invoke(bestParsed, ponderParsed);
	}

	private void ClearLastScores()
	{
		lock (_scoreLock)
		{
			_lastScoreMate = null;
			_lastScoreCp   = null;
		}
	}

	private void EnableOutputForwarding(int generation) =>
		Volatile.Write(ref _forwardingGeneration, generation);

	private void DisableOutputForwarding() =>
		Volatile.Write(ref _forwardingGeneration, 0);
}
