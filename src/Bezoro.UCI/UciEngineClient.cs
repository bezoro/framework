using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.UCI.API.Types;

namespace Bezoro.UCI;

internal sealed class UciEngineClient : IAsyncDisposable
{
	private static readonly Regex UciMoveRegex = new(
		@"^[a-h][1-8][a-h][1-8][qrbn]?$",
		RegexOptions.IgnoreCase | RegexOptions.Compiled);

	private readonly ConcurrentDictionary<Guid, (Func<string, bool> predicate, TaskCompletionSource<string> tcs)>
		_waiters = new();

	// Fast-path waiter count to avoid per-line scans when there are no waiters.
	private int _waiterCount;

	private readonly IUciTransport _transport;

	private CancellationTokenSource? _cts;

	// Engine activity state: 0 = Idle, 1 = Searching, 2 = Pondering
	private int _activity;

	private Task? _readerTask;

	public event Action<EngineActivity, EngineActivity>? ActivityChanged;
	public event Action<string, string>?                 BestMoveReceived;
	public event Action<PrincipalVariation>?             InfoPvReceived;
	public event Action<string>?                         LineReceived;

	public UciEngineClient(IUciTransport transport)
	{
		_transport = transport ?? throw new ArgumentNullException(nameof(transport));
	}

	public bool IsHealthy => _transport.IsHealthy;
	public bool IsStarted => _transport.IsStarted;

	public EngineActivity Activity => (EngineActivity)Volatile.Read(ref _activity);

	public ProcessUciTransport.TransportStatus Status => _transport.Status;

	// Session optimized path for search: avoids per-line predicate scans and event subscriptions.
	private sealed class SearchSession
	{
		public readonly ConcurrentQueue<string> Lines = new();
		public readonly TaskCompletionSource<string> BestMoveTcs =
			new(TaskCreationOptions.RunContinuationsAsynchronously);
		public readonly bool Ponder;

		public SearchSession(bool ponder)
		{
			Ponder = ponder;
		}
	}

	// Single active session is sufficient because UCI engines handle one search at a time.
	private volatile SearchSession? _activeSearch;

	public async Task GoFireAndForgetAsync(SearchParameters parameters, CancellationToken ct)
	{
		string cmd = BuildGoCommand(parameters);
		await _transport.WriteLineAsync(cmd, ct).ConfigureAwait(false);
		SetActivity(parameters.Ponder ? EngineActivity.Pondering : EngineActivity.Searching);
	}

	public async Task IsReadyAsync(CancellationToken ct)
	{
		await _transport.WriteLineAsync("isready", ct).ConfigureAwait(false);
		await WaitForLineAsync(
			l => l.Trim().Equals("readyok", StringComparison.OrdinalIgnoreCase),
			TimeSpan.FromSeconds(5),
			ct).ConfigureAwait(false);
	}

	public async Task SetOptionAsync(string name, string? value, CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(name)) return;

		string cmd = value is null ? $"setoption name {name}" : $"setoption name {name} value {value}";
		await _transport.WriteLineAsync(cmd, ct).ConfigureAwait(false);
	}

	public async Task SetPositionAsync(Fen fen, IEnumerable<string>? moves, CancellationToken ct)
	{
		if (!Fen.Validate(fen.Raw))
			throw new ArgumentException("Invalid FEN provided.", nameof(fen));

		string movePart = moves is { } m && m.Any() ? " moves " + string.Join(' ', m) : string.Empty;
		await _transport.WriteLineAsync($"position fen {fen.Raw}{movePart}", ct).ConfigureAwait(false);
	}

	public async Task StartAsync(CancellationToken ct = default)
	{
		await _transport.StartAsync(ct).ConfigureAwait(false);

		_cts        = new();
		_readerTask = Task.Run(ReadLoopAsync);

		await UciInitAsync(ct).ConfigureAwait(false);
		SetActivity(EngineActivity.Idle);
	}

	public async Task StopAsync(CancellationToken ct = default)
	{
		try
		{
			_cts?.Cancel();
			if (_readerTask is { })
				try
				{
					await _readerTask.ConfigureAwait(false);
				}
				catch
				{
					/* best-effort */
				}
		}
		finally
		{
			_readerTask = null;
			_cts?.Dispose();
			_cts = null;
		}

		await _transport.StopAsync(ct).ConfigureAwait(false);
		SetActivity(EngineActivity.Idle);
	}

	public async Task StopSearchAsync(CancellationToken ct)
	{
		await _transport.WriteLineAsync("stop", ct).ConfigureAwait(false);
		SetActivity(EngineActivity.Idle);
	}

	public async Task UciInitAsync(CancellationToken ct)
	{
		await _transport.WriteLineAsync("uci", ct).ConfigureAwait(false);
		await WaitForLineAsync(
			l => l.Trim().Equals("uciok", StringComparison.OrdinalIgnoreCase),
			TimeSpan.FromSeconds(5),
			ct).ConfigureAwait(false);

		await IsReadyAsync(ct).ConfigureAwait(false);
	}

	public async Task UciNewGameAsync(CancellationToken ct)
	{
		await _transport.WriteLineAsync("ucinewgame", ct).ConfigureAwait(false);
		await IsReadyAsync(ct).ConfigureAwait(false);
	}

	public async Task<Fen?> GetFenViaDAsync(CancellationToken ct)
	{
		await _transport.WriteLineAsync("d", ct).ConfigureAwait(false);

		string? fenLine = await WaitForLineAsync(
							  l => Fen.TryParseUciOutputLine(l, out _),
							  TimeSpan.FromSeconds(2),
							  ct).ConfigureAwait(false);

		return Fen.TryParseUciOutputLine(fenLine, out var fen) ? fen : null;
	}

	public async Task<IReadOnlyList<string>> GetLegalMovesViaGoPerft1Async(CancellationToken ct)
	{
		var results      = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		var movesDoneTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

		// Debounce completion after last move burst.
		CancellationTokenSource? debounce     = null;
		var                      moveCount    = 0;
		var                      fallbackSent = 0;
		object                   gate         = new();

		void ArmDebounce()
		{
			var old = Interlocked.Exchange(ref debounce, new());
			try
			{
				old?.Cancel();
			}
			catch
			{
				/* best-effort */
			}
			finally
			{
				old?.Dispose();
			}

			var local = debounce!;
			_ = Task.Run(async () =>
			{
				try
				{
					// Complete shortly after last move line; keeps latency low without fixed waits.
					await Task.Delay(100, local.Token).ConfigureAwait(false);
					movesDoneTcs.TrySetResult(true);
				}
				catch (OperationCanceledException)
				{
					// Debounce reset, ignore.
				}
				catch
				{
					// Keep silent; completion will be guarded elsewhere.
				}
			});
		}

		void CaptureMoves(string l)
		{
			var anyNew = false;

			foreach (string token in l.Split(
						 new[] { ' ', '\t', ',', ';', '|', ':' },
						 StringSplitOptions.RemoveEmptyEntries))
			{
				if (UciMoveRegex.IsMatch(token))
				{
					lock (gate)
					{
						if (results.Add(token.ToLowerInvariant()))
						{
							moveCount++;
							anyNew = true;
						}
					}
				}
			}

			if (anyNew)
			{
				ArmDebounce();
			}
		}

		LineReceived += CaptureMoves;
		try
		{
			await _transport.WriteLineAsync("goperft 1", ct).ConfigureAwait(false);

			// If nothing appears quickly, fall back to the more standard "go perft 1".
			_ = Task.Run(async () =>
			{
				try
				{
					await Task.Delay(120, ct).ConfigureAwait(false);
					if (Interlocked.CompareExchange(ref fallbackSent, 1, 0) == 0 &&
						Volatile.Read(ref moveCount) == 0 &&
						!ct.IsCancellationRequested)
						await _transport.WriteLineAsync("go perft 1", ct).ConfigureAwait(false);
				}
				catch
				{
					// Best-effort fallback.
				}
			});

			// Wait until we see moves and the output goes quiet briefly,
			// or until a small overall cap elapses to avoid hanging.
			var overallCap = Task.Delay(500, ct);
			var completed  = await Task.WhenAny(movesDoneTcs.Task, overallCap).ConfigureAwait(false);
			if (completed != movesDoneTcs.Task)
			{
				// Timed out waiting for quiet period; proceed with whatever we have.
			}
		}
		finally
		{
			LineReceived -= CaptureMoves;

			var d = Interlocked.Exchange(ref debounce, null);
			try
			{
				d?.Cancel();
			}
			catch
			{
				/* ignore */
			}
			finally
			{
				d?.Dispose();
			}
		}

		lock (gate)
		{
			return results.ToList();
		}
	}

	public async Task<SearchResult> GoAsync(SearchParameters parameters, CancellationToken ct)
	{
		string cmd = BuildGoCommand(parameters);

		// Create and publish the active session before sending the command to avoid races.
		var session = new SearchSession(parameters.Ponder);
		_activeSearch = session;

		// Derive a more sensible timeout:
		// - movetime: movetime + small buffer
		// - depth/nodes: longer ceiling
		// - infinite: generous cap (but still cancellable via ct)
		// - default: moderate timeout
		static TimeSpan ComputeTimeout(SearchParameters p)
		{
			if (p.MoveTimeMs is { } mt)
			{
				// add a small buffer to allow the engine to flush "bestmove"
				int buffered = Math.Clamp(mt + 750, 500, 60_000);
				return TimeSpan.FromMilliseconds(buffered);
			}

			if (p.Infinite) return TimeSpan.FromSeconds(120);

			if (p.Depth is { } d)
			{
				// conservative upper bound for depth-limited searches
				// (engines typically return much sooner)
				int sec = Math.Clamp((int)d * 2, 10, 90);
				return TimeSpan.FromSeconds(sec);
			}

			if (p.Nodes is { })
				// node-limited: allow a reasonably long cap
				return TimeSpan.FromSeconds(60);

			// time-controls (wtime/btime) or unspecified: moderate default
			return TimeSpan.FromSeconds(30);
		}

		var timeout = ComputeTimeout(parameters);

		await _transport.WriteLineAsync(cmd, ct).ConfigureAwait(false);
		SetActivity(parameters.Ponder ? EngineActivity.Pondering : EngineActivity.Searching);

		string? bestLine = null;
		// Await bestmove with timeout and cancellation without mutating the session TCS state.
		try
		{
			var bestTask = session.BestMoveTcs.Task;
			var timeoutTask = timeout == Timeout.InfiniteTimeSpan
								  ? Task.Delay(Timeout.Infinite, CancellationToken.None)
								  : Task.Delay(timeout);

			var       cancelTcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
			using var reg = ct.Register(static s => ((TaskCompletionSource<object?>)s!).TrySetResult(null), cancelTcs);

			var completed = await Task.WhenAny(bestTask, timeoutTask, cancelTcs.Task).ConfigureAwait(false);

			if (completed == cancelTcs.Task)
				throw new OperationCanceledException(ct);

			if (completed == bestTask)
			{
				bestLine = await bestTask.ConfigureAwait(false);
			}
			else
			{
				// Timeout path: try to stop and wait briefly for bestmove to arrive.
				try
				{
					await _transport.WriteLineAsync("stop", CancellationToken.None).ConfigureAwait(false);
				}
				catch
				{
					/* best-effort */
				}

				try
				{
					var graceCompleted = await Task.WhenAny(bestTask, Task.Delay(TimeSpan.FromSeconds(2)))
												   .ConfigureAwait(false);

					if (graceCompleted == bestTask)
					{
						bestLine = await bestTask.ConfigureAwait(false);
					}
					else
					{
						// Still no bestmove; if we captured one already, use it.
						bestLine = session.Lines.FirstOrDefault(l => l.StartsWith(
																	"bestmove ",
																	StringComparison.OrdinalIgnoreCase));

						if (bestLine is null)
							throw new TimeoutException("Engine search timed out without emitting 'bestmove'.");
					}
				}
				catch (OperationCanceledException) when (!ct.IsCancellationRequested)
				{
					// Treat as timeout in this context.
					bestLine = session.Lines.FirstOrDefault(l => l.StartsWith(
																"bestmove ",
																StringComparison.OrdinalIgnoreCase));
					if (bestLine is null) throw;
				}
			}
		}
		finally
		{
			// Ensure we flip to Idle; read loop will also do so on bestmove.
			SetActivity(EngineActivity.Idle);
		}

		// Ensure bestmove line is present in the buffer for parsing
		if (bestLine is { })
			session.Lines.Enqueue(bestLine);

		// Snapshot captured lines once; SearchResult.TryParse expects a stable enumerable
		var snapshot = session.Lines.ToList();

		// Clear the active session (defensive)
		if (ReferenceEquals(_activeSearch, session))
			_activeSearch = null;

		return SearchResult.TryParse(snapshot, out var result) ? result : default;
	}
	public async ValueTask DisposeAsync()
	{
		try
		{
			await StopAsync(CancellationToken.None).ConfigureAwait(false);
		}
		catch
		{
			/* best-effort */
		}

		if (_transport is IAsyncDisposable ad) await ad.DisposeAsync();
	}

	private static string BuildGoCommand(SearchParameters parameters)
	{
		var parts = new List<string> { "go" };

		if (parameters.Ponder) parts.Add("ponder");
		if (parameters.Infinite) parts.Add("infinite");
		if (parameters.WhiteTimeMs.HasValue) parts.Add($"wtime {parameters.WhiteTimeMs.Value}");
		if (parameters.BlackTimeMs.HasValue) parts.Add($"btime {parameters.BlackTimeMs.Value}");
		if (parameters.WhiteIncrementMs.HasValue) parts.Add($"winc {parameters.WhiteIncrementMs.Value}");
		if (parameters.BlackIncrementMs.HasValue) parts.Add($"binc {parameters.BlackIncrementMs.Value}");
		if (parameters.MoveTimeMs.HasValue) parts.Add($"movetime {parameters.MoveTimeMs.Value}");
		if (parameters.Nodes.HasValue) parts.Add($"nodes {parameters.Nodes.Value}");
		if (parameters.Depth.HasValue) parts.Add($"depth {parameters.Depth.Value}");
		if (parameters.Mate.HasValue) parts.Add($"mate {parameters.Mate.Value}");

		bool hasAnyLimit =
			parameters.Infinite ||
			parameters.Depth.HasValue ||
			parameters.Mate.HasValue ||
			parameters.MoveTimeMs.HasValue ||
			parameters.Nodes.HasValue ||
			parameters.WhiteTimeMs.HasValue ||
			parameters.BlackTimeMs.HasValue;

		if (!hasAnyLimit)
			parts.Add("depth 6");

		if (parameters.SearchMoves is { } sm)
		{
			var legal = sm.Where(s => UciMoveRegex.IsMatch(s))
						  .Select(s => s.ToLowerInvariant());

			if (legal.Any()) parts.Add("searchmoves " + string.Join(' ', legal));
		}

		return string.Join(' ', parts);
	}

	private async Task ReadLoopAsync()
	{
		var token = _cts?.Token ?? CancellationToken.None;

		try
		{
			await foreach (string line in _transport.ReadLinesAsync(token).ConfigureAwait(false))
			{
				try
				{
					LineReceived?.Invoke(line);

					// Fast-path: capture search session lines without scanning waiter predicates
					var sess = _activeSearch;

					if (line.StartsWith("info ", StringComparison.OrdinalIgnoreCase))
					{
						if (PrincipalVariation.TryParse(line, out var pv))
							InfoPvReceived?.Invoke(pv);

						// Capture in active session (if any)
						sess?.Lines.Enqueue(line);
					}

					if (line.StartsWith("bestmove ", StringComparison.OrdinalIgnoreCase))
					{
						string[]? tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
						string    best   = tokens.Length > 1 ? tokens[1] : string.Empty;
						string    ponder = tokens.Length > 3 && tokens[2] == "ponder" ? tokens[3] : string.Empty;

						// Capture in active session before notifying
						if (sess is { })
						{
							sess.Lines.Enqueue(line);
							// Complete the session bestmove waiter; ignore if already completed.
							sess.BestMoveTcs.TrySetResult(line);
							_activeSearch = null;
						}

						SetActivity(EngineActivity.Idle);
						BestMoveReceived?.Invoke(best, ponder);
					}

					// Existing generic waiters (control messages like 'uciok', 'readyok')
					if (Volatile.Read(ref _waiterCount) != 0)
					{
						foreach (var kv in _waiters)
						{
							var w = kv.Value;

							bool match;
							try
							{
								match = w.predicate(line);
							}
							catch
							{
								match = false;
							}

							if (match)
								if (_waiters.TryRemove(kv.Key, out var removed))
								{
									Interlocked.Decrement(ref _waiterCount);
									removed.tcs.TrySetResult(line);
								}
						}
					}
				}
				catch
				{
					// keep loop alive
				}
			}
		}
		catch
		{
			// gracefully end
		}

		// Cancel any pending waiters
		foreach (var kv in _waiters)
		{
			if (_waiters.TryRemove(kv.Key, out var w))
			{
				Interlocked.Decrement(ref _waiterCount);
				w.tcs.TrySetCanceled();
			}
		}

		// Fail any active search session if the stream ends unexpectedly
		_activeSearch?.BestMoveTcs.TrySetCanceled();
		_activeSearch = null;

		// Ensure we are not stuck in a non-idle state if the read loop ends
		SetActivity(EngineActivity.Idle);
	}

	private async Task<string> WaitForLineAsync(Func<string, bool> predicate, TimeSpan timeout, CancellationToken ct)
	{
		var id  = Guid.NewGuid();
		var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
		if (_waiters.TryAdd(id, (predicate, tcs))) Interlocked.Increment(ref _waiterCount);

		using var timeoutCts = timeout == Timeout.InfiniteTimeSpan ? null : new CancellationTokenSource(timeout);
		using var linked = timeoutCts is null
							   ? CancellationTokenSource.CreateLinkedTokenSource(ct)
							   : CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

		using var reg = linked.Token.Register(() => tcs.TrySetCanceled());

		try
		{
			return await tcs.Task.ConfigureAwait(false);
		}
		finally
		{
			if (_waiters.TryRemove(id, out _)) Interlocked.Decrement(ref _waiterCount);
		}
	}

	private void SetActivity(EngineActivity next)
	{
		var previous = (EngineActivity)Interlocked.Exchange(ref _activity, (int)next);
		if (previous == next) return;

		try
		{
			ActivityChanged?.Invoke(previous, next);
		}
		catch
		{
			/* swallow */
		}
	}
}

public enum EngineActivity
{
	Idle      = 0,
	Searching = 1,
	Pondering = 2
}
