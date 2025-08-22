using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.UCI.API.Types;

namespace Bezoro.UCI;

internal sealed class MoveClassificationEngine(
	string               enginePath,
	IEnumerable<string>? args             = null,
	string?              workingDirectory = null
) : IAsyncDisposable
{
	private readonly IEnumerable<string>? _args = args;

	private readonly QuickInfoEngine _quick = new(enginePath, args, workingDirectory);

	private readonly string _enginePath = enginePath ?? throw new ArgumentNullException(nameof(enginePath));

	private ProcessUciTransport? _transport;
	private UciEngineClient?     _client;

	public async IAsyncEnumerable<(string Move, MoveAnalysis Analysis, MoveScore Score)> ClassifyAsync(
		Fen                                        fen,
		BoardState                                 board,
		uint                                       perMoveDepth = 6,
		[EnumeratorCancellation] CancellationToken ct           = default)
	{
		// Fetch legal moves using the quick engine
		await _quick.SetPositionAsync(fen, null, ct);
		var legalMoves = await _quick.GetLegalMovesAsync(ct).ConfigureAwait(false);

		foreach (string? move in legalMoves)
		{
			if (move is null) continue;

			(string Move, MoveAnalysis Analysis, MoveScore Score)? resultTuple = null;

			try
			{
				await _client.SetPositionAsync(fen, [move], ct).ConfigureAwait(false);
				// Restrict search to the specific move using UCI searchmoves
				var result = await _client.GoAsync(new() { Depth = perMoveDepth, SearchMoves = [move] }, ct)
										  .ConfigureAwait(false);

				var score = ScoreFromResult(result);
				// Determine terminal positions (mate/stalemate) by checking opponent replies after our move.
				var noMoves = false;
				try
				{
					await _quick.SetPositionAsync(fen, [move], ct).ConfigureAwait(false);
					var replies = await _quick.GetLegalMovesAsync(ct).ConfigureAwait(false);
					noMoves = replies is null || replies.Count == 0;
				}
				catch
				{
					// best-effort: if quick check fails, fall back to score-based inference only
				}

				// If terminal and engine indicates mate,
				// normalize to mate -1 so MoveAnalysis flags mate/check reliably.
				bool engineThinksMate = score.ScoreMate.HasValue
											? score.ScoreMate.Value != 0
											: result.HasMate;

				bool isMate      = noMoves && engineThinksMate;
				bool isStalemate = noMoves && !engineThinksMate;

				if (isMate && score.ScoreMate is not -1)
					score = MoveScore.FromMate(-1);

				var analysis = MoveAnalysis.Analyze(move, board, score, isStalemate);

				resultTuple = (move, analysis, score);
			}
			catch
			{
				/* best-effort: skip move on error */
			}

			if (resultTuple.HasValue)
				yield return resultTuple.Value;
		}
	}

	public async Task<(string Move, MoveAnalysis Analysis, MoveScore Score)?> ClassifyMoveAsync(
		Fen               fen,
		BoardState        board,
		string            move,
		uint              perMoveDepth = 6,
		CancellationToken ct           = default)
	{
		// Validate that the move is legal in the given position
		try
		{
			await _quick.SetPositionAsync(fen, null, ct).ConfigureAwait(false);
			var legalMoves = await _quick.GetLegalMovesAsync(ct).ConfigureAwait(false);

			if (legalMoves is null || legalMoves.All(m => m != move))
				return null;

			await _client.SetPositionAsync(fen, null, ct).ConfigureAwait(false);

			// Restrict search to the specific move using UCI searchmoves
			var result = await _client.GoAsync(new() { Depth = perMoveDepth, SearchMoves = [move] }, ct)
									  .ConfigureAwait(false);

			var score = ScoreFromResult(result);

			// Determine terminal positions (mate/stalemate) by checking opponent replies after our move.
			var noMoves = false;
			try
			{
				await _quick.SetPositionAsync(fen, [move], ct).ConfigureAwait(false);
				var replies = await _quick.GetLegalMovesAsync(ct).ConfigureAwait(false);
				noMoves = replies is null || replies.Count == 0;
			}
			catch
			{
				// best-effort: if quick check fails, fall back to score-based inference only
			}

			// If terminal and engine indicates mate,
			// normalize to mate -1 so MoveAnalysis flags mate/check reliably.
			bool engineThinksMate = score.ScoreMate.HasValue
										? score.ScoreMate.Value != 0
										: result.HasMate;

			bool isMate      = noMoves && engineThinksMate;
			bool isStalemate = noMoves && !engineThinksMate;

			if (isMate && score.ScoreMate is not -1)
				score = MoveScore.FromMate(-1);

			var analysis = MoveAnalysis.Analyze(move, board, score, isStalemate);

			return (move, analysis, score);
		}
		catch
		{
			// best-effort: return null on error
			return null;
		}
	}

	public async Task StartAsync(CancellationToken ct = default)
	{
		await _quick.StartAsync(ct).ConfigureAwait(false);

		_transport = new(_enginePath, _args, workingDirectory);
		_client    = new(_transport);
		await _client.StartAsync(ct).ConfigureAwait(false);
		await _client.SetOptionAsync("Threads", "2", ct).ConfigureAwait(false);
		await _client.SetOptionAsync("MultiPv", "3", ct).ConfigureAwait(false);
	}

	public async Task StopAsync(CancellationToken ct = default)
	{
		await _quick.StopAsync(ct).ConfigureAwait(false);

		if (_client is { })
		{
			try
			{
				await _client.StopAsync(ct).ConfigureAwait(false);
			}
			catch
			{
				/* best-effort */
			}
		}
	}

	public async ValueTask DisposeAsync()
	{
		await _quick.DisposeAsync();

		if (_client is { })
		{
			try
			{
				await _client.DisposeAsync();
			}
			catch
			{
				/* best-effort */
			}

			_client = null;
		}

		_transport = null;
	}

	private static MoveScore ScoreFromResult(SearchResult result)
	{
		if (result is { HasMate: true, MateScore: { } })
			return MoveScore.FromMate(result.MateScore.Value);

		int? cp = result.BestCpScore ?? result.PrincipalVariations.FirstOrDefault().ScoreCp;
		return cp.HasValue ? MoveScore.FromCp(cp.Value) : default;
	}
}
