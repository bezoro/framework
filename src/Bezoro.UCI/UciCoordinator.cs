using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.UCI.API.Types;

namespace Bezoro.UCI;

internal sealed class UciCoordinator : IAsyncDisposable
{
	private readonly MoveClassificationEngine _classifier;
	private readonly PonderEngine             _ponder;
	private readonly QuickInfoEngine          _quick;

	public event Action<string, string>? PonderBestMove;

	public event Action<PrincipalVariation>? PonderInfo;

	public UciCoordinator(string enginePath, IEnumerable<string>? args = null, string? workingDirectory = null)
	{
		_quick      = new(enginePath, args, workingDirectory);
		_ponder     = new(enginePath, args, workingDirectory);
		_classifier = new(enginePath, args, workingDirectory);

		_ponder.InfoPv   += pv => PonderInfo?.Invoke(pv);
		_ponder.BestMove += (b, p) => PonderBestMove?.Invoke(b, p);
	}

	public IAsyncEnumerable<(string Move, MoveAnalysis Analysis, MoveScore Score)> ClassifyMovesAsync(
		Fen               fen,
		BoardState        board,
		uint              perMoveDepth  = 6,
		int               maxConcurrent = 2,
		CancellationToken ct            = default)
		=> _classifier.ClassifyAsync(fen, board, perMoveDepth, ct);

	public async Task StartAsync(CancellationToken ct = default)
	{
		await Task.WhenAll(
			_quick.StartAsync(ct),
			_ponder.StartAsync(ct),
			_classifier.StartAsync(ct)
		).ConfigureAwait(false);
	}

	public Task StartPonderAsync(Fen fen, IEnumerable<string>? playedMoves, CancellationToken ct = default) =>
		_ponder.StartPonderAsync(fen, playedMoves, ct);

	public async Task StopAsync(CancellationToken ct = default)
	{
		await Task.WhenAll(
			_classifier.StopAsync(ct),
			_ponder.StopAsync(ct),
			_quick.StopAsync(ct)
		).ConfigureAwait(false);
	}

	public Task StopPonderAsync(CancellationToken ct = default) =>
		_ponder.StopPonderAsync(ct);

	// Position update: stop ponder, then restart ponder on the new position.
	public async Task UpdatePositionAsync(
		Fen                  fen,
		IEnumerable<string>? playedMoves,
		BoardState           board,
		CancellationToken    ct = default)
	{
		await _ponder.StopPonderAsync(ct).ConfigureAwait(false);
		_ = _ponder.StartPonderAsync(fen, playedMoves, ct);
	}

	// Convenience proxies

	public Task<Fen?> GetCurrentFenAsync(CancellationToken ct = default) =>
		_quick.GetCurrentFenAsync(ct);

	public Task<IReadOnlyList<string>> GetLegalMovesAsync(CancellationToken ct = default) =>
		_quick.GetLegalMovesAsync(ct);

	public async ValueTask DisposeAsync()
	{
		await _classifier.DisposeAsync();
		await _ponder.DisposeAsync();
		await _quick.DisposeAsync();
	}
}
