using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Bezoro.UCI.API.Types;

namespace Bezoro.UCI;

internal sealed class MoveClassificationEngine(
	string               enginePath,
	IEnumerable<string>? args             = null,
	string?              workingDirectory = null
) : IAsyncDisposable
{
	private readonly QuickInfoEngine _quick      = new(enginePath, args, workingDirectory);
	private readonly string          _enginePath = enginePath ?? throw new ArgumentNullException(nameof(enginePath));

	public async IAsyncEnumerable<(string Move, MoveAnalysis Analysis, MoveScore Score)> ClassifyAsync(
		Fen                                        fen,
		BoardState                                 board,
		int                                        perMoveDepth  = 6,
		int                                        maxConcurrent = 2,
		[EnumeratorCancellation] CancellationToken ct            = default)
	{
		// Fetch legal moves using the quick engine
		await _quick.SetPositionAsync(fen, null, ct);
		var legalMoves = await _quick.GetLegalMovesAsync(ct).ConfigureAwait(false);

		// Build a small pool of UCI clients for parallel scoring
		maxConcurrent = Math.Max(1, maxConcurrent);
		var pool = Channel.CreateBounded<UciEngineClient>(
			new BoundedChannelOptions(maxConcurrent)
				{ SingleReader = false, SingleWriter = false, FullMode = BoundedChannelFullMode.Wait });

		var clients = new List<UciEngineClient>(maxConcurrent);

		try
		{
			for (var i = 0; i < maxConcurrent; i++)
			{
				var transport = new ProcessUciTransport(_enginePath, args, workingDirectory);
				var client    = new UciEngineClient(transport);
				await client.StartAsync(ct).ConfigureAwait(false);
				clients.Add(client);
				await pool.Writer.WriteAsync(client, ct).ConfigureAwait(false);
			}

			var throttledCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

			var tasks = new List<Task<(string Move, MoveAnalysis Analysis, MoveScore Score)?>>(legalMoves.Count);
			foreach (string? move in legalMoves)
			{
				tasks.Add(
					Task.Run<(string Move, MoveAnalysis Analysis, MoveScore Score)?>(
						async () =>
						{
							UciEngineClient client;
							try
							{
								client = await pool.Reader.ReadAsync(throttledCts.Token).ConfigureAwait(false);
							}
							catch
							{
								return null;
							}

							try
							{
								await client.SetPositionAsync(fen, new[] { move }, throttledCts.Token)
											.ConfigureAwait(false);

								var result = await client.GoAsync(new() { Depth = perMoveDepth }, throttledCts.Token)
														 .ConfigureAwait(false);

								var score = ScoreFromResult(result);

								// Ask the engine for legal replies in the resulting position
								var legalNext = await client.GetLegalMovesViaGoPerft1Async(throttledCts.Token)
															.ConfigureAwait(false);

								// First pass: compute check information (without claiming stalemate yet)
								var provisional = MoveAnalysis.Analyze(move, board, score, false);

								bool noMoves     = legalNext.Count == 0;
								bool isCheckNow  = provisional.IsCheck;
								bool isMate      = noMoves && isCheckNow;
								bool isStalemate = noMoves && !isCheckNow;

								// Normalize score for terminal checkmate so downstream logic can infer IsMate reliably.
								if (isMate && (!score.ScoreMate.HasValue || score.ScoreMate.Value != 0))
									score = MoveScore.FromMate(0);

								// Final analysis with correct stalemate flag
								var analysis = MoveAnalysis.Analyze(move, board, score, isStalemate);

								return (move, analysis, score);
							}
							catch
							{
								return null;
							}
							finally
							{
								try
								{
									await pool.Writer.WriteAsync(client, throttledCts.Token).ConfigureAwait(false);
								}
								catch
								{
									/* best-effort */
								}
							}
						},
						throttledCts.Token));
			}

			while (tasks.Count > 0)
			{
				var finished = await Task.WhenAny(tasks).ConfigureAwait(false);
				tasks.Remove(finished);
				var tuple = await finished.ConfigureAwait(false);
				if (tuple.HasValue) yield return tuple.Value;
			}
		}
		finally
		{
			// Tear down client pool
			foreach (var c in clients)
			{
				try
				{
					await c.StopAsync(CancellationToken.None).ConfigureAwait(false);
				}
				catch
				{
					/* best-effort */
				}

				try
				{
					await c.DisposeAsync();
				}
				catch
				{
					/* best-effort */
				}
			}
		}
	}

	public async Task StartAsync(CancellationToken ct = default)
	{
		await _quick.StartAsync(ct).ConfigureAwait(false);
	}

	public async Task StopAsync(CancellationToken ct = default)
	{
		await _quick.StopAsync(ct).ConfigureAwait(false);
	}

	public async ValueTask DisposeAsync()
	{
		await _quick.DisposeAsync();
	}

	private static MoveScore ScoreFromResult(SearchResult result)
	{
		if (result.HasMate && result.MateScore.HasValue)
			return MoveScore.FromMate(result.MateScore.Value);

		int? cp = result.BestCpScore ?? result.PrincipalVariations.FirstOrDefault().ScoreCp;
		return cp.HasValue ? MoveScore.FromCp(cp.Value) : default;
	}
}
