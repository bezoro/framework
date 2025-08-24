using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.UCI.API.Types;
using Bezoro.UCI.Domain;

namespace Bezoro.UCI.API;

public sealed class UciCoordinator : IAsyncDisposable
{
	private readonly MoveClassificationEngine _classifier;

	private readonly object          _cacheLock = new();
	private readonly PonderEngine    _ponder;
	private readonly QuickInfoEngine _quick;
	private          string?         _lastPonderKey;

	public event Action<string, string>?     PonderBestMove;
	public event Action<PrincipalVariation>? PonderInfo;

	public UciCoordinator(string enginePath, IEnumerable<string>? args = null, string? workingDirectory = null)
	{
		_quick      = new(enginePath, args, workingDirectory);
		_ponder     = new(enginePath, args, workingDirectory);
		_classifier = new(enginePath, args, workingDirectory);

		_ponder.InfoPv   += pv => PonderInfo?.Invoke(pv);
		_ponder.BestMove += (b, p) => PonderBestMove?.Invoke(b, p);
	}

	public IAsyncEnumerable<Move> ClassifyMovesAsync(
		Fen               fen,
		uint              perMoveDepth = 6,
		CancellationToken ct           = default)
		=> _classifier.ClassifyAsync(fen, perMoveDepth, ct);

	public async Task NewGameAsync(CancellationToken ct = default)
	{
		await Task.WhenAll(
			_classifier.NewGameAsync(ct),
			_ponder.NewGameAsync(ct),
			_quick.NewGameAsync(ct)
		).ConfigureAwait(false);

		// Clear cached state for new game
		lock (_cacheLock)
		{
			_lastPonderKey = null;
		}
	}

	public async Task StartAsync(CancellationToken ct = default)
	{
		await Task.WhenAll(
			_quick.StartAsync(ct),
			_ponder.StartAsync(ct),
			_classifier.StartAsync(ct)
		).ConfigureAwait(false);

		// Clear cached state at the start of a session
		lock (_cacheLock)
		{
			_lastPonderKey = null;
		}
	}

	public Task StartPonderAsync(Fen fen, IEnumerable<string>? playedMoves, CancellationToken ct = default)
	{
		string key = BuildPositionKey(fen, playedMoves);
		lock (_cacheLock)
		{
			if (string.Equals(_lastPonderKey, key, StringComparison.Ordinal))
				// Already pondering this position; skip restart
				return Task.CompletedTask;

			_lastPonderKey = key;
		}

		return _ponder.StartPonderAsync(fen, playedMoves, ct);
	}

	public async Task StopAsync(CancellationToken ct = default)
	{
		await Task.WhenAll(
			_classifier.StopAsync(ct),
			_ponder.StopAsync(ct),
			_quick.StopAsync(ct)
		).ConfigureAwait(false);

		// Clear cached state on stop
		lock (_cacheLock)
		{
			_lastPonderKey = null;
		}
	}

	public async Task StopPonderAsync(CancellationToken ct = default)
	{
		await _ponder.StopPonderAsync(ct).ConfigureAwait(false);
		// Clear cached ponder key so future identical requests are not skipped
		lock (_cacheLock)
		{
			_lastPonderKey = null;
		}
	}

	public async Task UpdatePositionAsync(
		Fen                  fen,
		IEnumerable<string>? playedMoves,
		CancellationToken    ct = default)
	{
		string key = BuildPositionKey(fen, playedMoves);
		lock (_cacheLock)
		{
			if (string.Equals(_lastPonderKey, key, StringComparison.Ordinal))
				// No change in position; keep current pondering
				return;
		}

		await StopPonderAsync(ct).ConfigureAwait(false);
		_ = StartPonderAsync(fen, playedMoves, ct);
	}

	public async Task<bool> IsMatchFinishedAsync(CancellationToken ct = default)
	{
		var legalMoves = await _quick.GetLegalMovesAsync(ct).ConfigureAwait(false);
		return legalMoves.Count == 0;
	}

	public Task<Fen?> GetCurrentFenAsync(CancellationToken ct = default) =>
		_quick.GetCurrentFenAsync(ct);

	public Task<IReadOnlyCollection<string>> GetLegalMovesAsync(CancellationToken ct = default) =>
		_quick.GetLegalMovesAsync(ct);

	public async ValueTask DisposeAsync()
	{
		await _classifier.DisposeAsync();
		await _ponder.DisposeAsync();
		await _quick.DisposeAsync();
	}

	private static string BuildPositionKey(Fen fen, IEnumerable<string>? playedMoves)
	{
		string movesJoined = playedMoves is null ? string.Empty : string.Join(' ', playedMoves);
		return $"{fen}|{movesJoined}";
	}
}
