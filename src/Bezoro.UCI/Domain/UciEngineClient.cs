using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.UCI.API.Types;
using Bezoro.UCI.Domain.Common.Constants;

namespace Bezoro.UCI.Domain;

/// <summary>
/// Provides high-level async orchestration and lifecycle management for a UCI engine process/transport.
/// Maintains a single search session at a time, parses UCI output, tracks engine activity state, and
/// exposes events and helpers for UCI clients.
/// </summary>
internal sealed class UciEngineClient(IUciTransport transport) : IAsyncDisposable
{
	/// <summary>
	/// Maps waiter tokens to asynchronous completion sources for awaiting specific output lines.
	/// Used to bridge imperative await-points (e.g. wait for "uciok", "readyok") with the background 
	/// read loop.
	/// </summary>
	private readonly ConcurrentDictionary<Guid, (Func<string, bool> predicate, TaskCompletionSource<string> tcs)>
		_waiters = new();

	/// <summary>
	/// Underlying transport abstraction for communicating with the engine.
	/// </summary>
	private readonly IUciTransport _transport = transport ?? throw new ArgumentNullException(nameof(transport));

	/// <summary>
	/// Cancellation source for the read loop and engine lifetime.
	/// </summary>
	private CancellationTokenSource? _cts;

	/// <summary>
	/// Atomic int representing current engine activity state (see <see cref="EngineActivity"/>).
	/// </summary>
	private int _activity;

	/// <summary>
	/// Fast-track count for waiter presence, avoiding dictionary scans when there are no waiters.
	/// </summary>
	private int _waiterCount;

	/// <summary>
	/// Current <see cref="SearchSession"/> tracked for an ongoing search operation.
	/// Only one search is permitted at a time.
	/// </summary>
	private volatile SearchSession? _activeSearch;

	/// <summary>
	/// Holds background read loop Task.
	/// </summary>
	private Task? _readerTask;

	/// <summary>
	/// Occurs when the engine transitions between <see cref="EngineActivity"/> states.
	/// </summary>
	public event Action<EngineActivity, EngineActivity>? ActivityChanged;
	/// <summary>
	/// Notifies when the engine emits a "bestmove" line.
	/// </summary>
	public event Action<string, string>?                 BestMoveReceived;
	/// <summary>
	/// Raised for principal variation ("info ... pv ...") lines.
	/// </summary>
	public event Action<PrincipalVariation>?             InfoPvReceived;
	/// <summary>
	/// Raised for every output line received from the engine.
	/// </summary>
	public event Action<string>?                         LineReceived;

	/// <summary>
	/// Indicates whether the underlying transport is healthy.
	/// </summary>
	public bool IsHealthy => _transport.IsHealthy;
	/// <summary>
	/// Returns true after <see cref="StartAsync"/> is called and until <see cref="StopAsync"/> completes.
	/// </summary>
	public bool IsStarted => _transport.IsStarted;

	/// <summary>
	/// Gets the current engine activity state.
	/// </summary>
	public EngineActivity Activity => (EngineActivity)Volatile.Read(ref _activity);

	/// <summary>
	/// Engine process/transport status.
	/// </summary>
	public ProcessUciTransport.TransportStatus Status => _transport.Status;

	/// <summary>
	/// Determines if a string is a valid UCI move (e.g. "e2e4", "a7a8q").
	/// </summary>
	public static bool IsUciMoveString(string s)
	{
		var span = s.AsSpan();
		if ((uint)span.Length - 4 > 1) return false;

		if (!IsFile(span[0]) || !IsRank(span[1]) || !IsFile(span[2]) || !IsRank(span[3])) return false;

		return span.Length == 4 || IsPromo(span[4]);

		static bool IsFile(char c) => (uint)((c | 0x20) - 'a') <= 7;

		static bool IsRank(char c) => (uint)(c - '1') <= 7;

		static bool IsPromo(char c)
		{
			int lc = c | 0x20;
			return lc is 'q' or 'r' or 'b' or 'n';
		}
	}

	/// <summary>
	/// Builds a UCI-compliant "go ..." command from <paramref name="parameters"/>.
	/// </summary>
	/// <param name="parameters">Search configuration</param>
	/// <returns>Full "go ..." line to send to engine</returns>
	public static string BuildGoCommand(SearchParameters parameters)
	{
		var parts = new List<string> { UciConstants.Commands.GO };

		if (parameters.Ponder) parts.Add(UciConstants.Parameters.PONDER);
		if (parameters.Infinite) parts.Add(UciConstants.Parameters.INFINITE);
		if (parameters.WhiteTimeMs.HasValue)
			parts.Add($"{UciConstants.Parameters.WHITE_TIME} {parameters.WhiteTimeMs.Value}");

		if (parameters.BlackTimeMs.HasValue)
			parts.Add($"{UciConstants.Parameters.BLACK_TIME} {parameters.BlackTimeMs.Value}");

		if (parameters.WhiteIncrementMs.HasValue)
			parts.Add($"{UciConstants.Parameters.WHITE_TIME_INCREMENT} {parameters.WhiteIncrementMs.Value}");

		if (parameters.BlackIncrementMs.HasValue)
			parts.Add($"{UciConstants.Parameters.BLACK_TIME_INCREMENT} {parameters.BlackIncrementMs.Value}");

		if (parameters.MoveTimeMs.HasValue)
			parts.Add($"{UciConstants.Parameters.MOVE_TIME} {parameters.MoveTimeMs.Value}");

		if (parameters.Nodes.HasValue) parts.Add($"{UciConstants.Parameters.NODES} {parameters.Nodes.Value}");
		if (parameters.Depth.HasValue) parts.Add($"{UciConstants.Parameters.DEPTH} {parameters.Depth.Value}");
		if (parameters.Mate.HasValue) parts.Add($"{UciConstants.Parameters.MATE} {parameters.Mate.Value}");

		bool hasAnyLimit =
			parameters.Infinite ||
			parameters.Depth.HasValue ||
			parameters.Mate.HasValue ||
			parameters.MoveTimeMs.HasValue ||
			parameters.Nodes.HasValue ||
			parameters.WhiteTimeMs.HasValue ||
			parameters.BlackTimeMs.HasValue;

		if (!hasAnyLimit)
			parts.Add($"{UciConstants.Parameters.DEPTH} 6");

		if (parameters.SearchMoves is not { } sm) return string.Join(' ', parts);

		List<string>? legal = null;
		foreach (string? s in sm)
		{
			if (string.IsNullOrEmpty(s)) continue;
			if (!IsUciMoveString(s)) continue;

			legal ??= [];
			legal.Add(s.ToLowerInvariant());
		}

		if (legal is { Count: > 0 }) parts.Add($"{UciConstants.Parameters.SEARCH_MOVES} " + string.Join(' ', legal));

		return string.Join(' ', parts);
	}

	/// <summary>
	/// Starts a search with the given parameters and does not await engine termination nor the bestmove response.
	/// Use for fire-and-forget searches (e.g., GUI spinning).
	/// </summary>
	public async Task GoFireAndForgetAsync(SearchParameters parameters, CancellationToken ct)
	{
		string cmd = BuildGoCommand(parameters);
		await _transport.WriteLineAsync(cmd, ct).ConfigureAwait(false);
		SetActivity(parameters.Ponder ? EngineActivity.Pondering : EngineActivity.Searching);
	}

	/// <summary>
	/// Sends "isready" to the engine and waits up to 10 seconds for "readyok".
	/// </summary>
	public async Task IsReadyAsync(CancellationToken ct)
	{
		var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

		LineReceived += Handler;
		using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
		using var linked     = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
		using var reg        = linked.Token.Register(() => tcs.TrySetCanceled(linked.Token));
		try
		{
			await _transport.WriteLineAsync(UciConstants.Commands.IS_READY, ct).ConfigureAwait(false);
			await tcs.Task.ConfigureAwait(false);
		}
		finally
		{
			LineReceived -= Handler;
		}

		return;

		/// <summary>
		/// Completion handler for "readyok" response.
		/// </summary>
		void Handler(string l)
		{
			try
			{
				if (l.Trim().Equals(UciConstants.Responses.READY_OK, StringComparison.OrdinalIgnoreCase))
					tcs.TrySetResult(l);
			}
			catch
			{
				/* ignore */
			}
		}
	}

	/// <summary>
	/// Sends a "setoption" command to the engine.
	/// </summary>
	public async Task SetOptionAsync(string name, string? value, CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(name)) return;

		string cmd = value is null
						 ? $"{UciConstants.Commands.SET_OPTION} {UciConstants.Keywords.NAME} {name}"
						 : $"{UciConstants.Commands.SET_OPTION} {UciConstants.Keywords.NAME} {name} {UciConstants.Keywords.VALUE} {value}";

		await _transport.WriteLineAsync(cmd, ct).ConfigureAwait(false);
	}

	/// <summary>
	/// Sets the board position using a FEN and (optionally) a move list.
	/// </summary>
	public async Task SetPositionAsync(Fen fen, IEnumerable<string>? moves, CancellationToken ct)
	{
		if (!Fen.Validate(fen.Raw))
			throw new ArgumentException("Invalid FEN provided.", nameof(fen));

		string movePart = moves != null && moves.Any()
							  ? $"{UciConstants.Keywords.MOVES} " + string.Join(' ', moves)
							  : string.Empty;

		await _transport.WriteLineAsync(
			$"{UciConstants.Commands.POSITION} {UciConstants.Keywords.FEN} {fen.Raw} {movePart}",
			ct).ConfigureAwait(false);
	}

	/// <summary>
	/// Starts the engine process/connection, background read loop, and completes UCI handshake.
	/// </summary>
	public async Task StartAsync(CancellationToken ct = default)
	{
		await _transport.StartAsync(ct).ConfigureAwait(false);

		_cts = new();
		// ReSharper disable once MethodSupportsCancellation
		_readerTask = Task.Run(ReadLoopAsync);

		await UciInitAsync(ct).ConfigureAwait(false);
		SetActivity(EngineActivity.Idle);
	}

	/// <summary>
	/// Gracefully stops the engine, read loop, and disposes transports.
	/// </summary>
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

	/// <summary>
	/// Sends the "stop" command to the engine and immediately transitions to Idle.
	/// </summary>
	public async Task StopSearchAsync(CancellationToken ct)
	{
		await _transport.WriteLineAsync(UciConstants.Commands.STOP, ct).ConfigureAwait(false);
		SetActivity(EngineActivity.Idle);
	}

	/// <summary>
	/// Sends "uci" to the engine and waits for "uciok" and "readyok" to confirm supported handshaking.
	/// </summary>
	public async Task UciInitAsync(CancellationToken ct)
	{
		var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

		LineReceived += Handler;
		using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
		using var linked     = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
		using var reg        = linked.Token.Register(() => tcs.TrySetCanceled(linked.Token));
		try
		{
			await _transport.WriteLineAsync(UciConstants.Commands.UCI, ct).ConfigureAwait(false);
			await tcs.Task.ConfigureAwait(false);
		}
		finally
		{
			LineReceived -= Handler;
		}

		await IsReadyAsync(ct).ConfigureAwait(false);
		return;

		/// <summary>
		/// Handler sets completion for "uciok" response.
		/// </summary>
		void Handler(string l)
		{
			try
			{
				if (l.Trim().Equals(UciConstants.Responses.UCI_OK, StringComparison.OrdinalIgnoreCase))
					tcs.TrySetResult(l);
			}
			catch
			{
				/* ignore */
			}
		}
	}

	/// <summary>
	/// Informs the engine of a new game context via "ucinewgame"; calls <see cref="IsReadyAsync"/>.
	/// </summary>
	public async Task UciNewGameAsync(CancellationToken ct)
	{
		await _transport.WriteLineAsync(UciConstants.Commands.UCI_NEW_GAME, ct).ConfigureAwait(false);
		await IsReadyAsync(ct).ConfigureAwait(false);
	}

	/// <summary>
	/// Requests the current engine FEN using the "d" command and parses "fen ..." and "checkers ..." output.
	/// </summary>
	public async Task<Fen?> GetFenViaDAsync(CancellationToken ct)
	{
		// Capture 'fen' and 'checkers' lines from the 'd' output to avoid races.
		var fenTcs      = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
		var checkersTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

		void Handler(string line)
		{
			if (string.IsNullOrEmpty(line)) return;

			string? trimmed = line.TrimStart();

			if (!fenTcs.Task.IsCompleted &&
				trimmed.StartsWith(UciConstants.Prefixes.FEN, StringComparison.OrdinalIgnoreCase))
				fenTcs.TrySetResult(line);

			if (!checkersTcs.Task.IsCompleted &&
				trimmed.StartsWith(UciConstants.Prefixes.CHECKERS, StringComparison.OrdinalIgnoreCase))
				checkersTcs.TrySetResult(line);
		}

		LineReceived += Handler;
		try
		{
			await _transport.WriteLineAsync(UciConstants.Commands.DISPLAY_BOARD, ct).ConfigureAwait(false);

			// Await the FEN line first
			string? fenLine = null;
			var fenCompleted = await Task.WhenAny(fenTcs.Task, Task.Delay(TimeSpan.FromSeconds(2)))
										 .ConfigureAwait(false);

			ct.ThrowIfCancellationRequested();

			if (fenCompleted == fenTcs.Task)
				fenLine = await fenTcs.Task.ConfigureAwait(false);

			if (fenLine is null)
				return null;

			// Give a short window for the 'checkers' line to arrive after the FEN line
			string? checkersLine = null;
			var checkersCompleted = await Task.WhenAny(checkersTcs.Task, Task.Delay(TimeSpan.FromMilliseconds(750)))
											  .ConfigureAwait(false);

			ct.ThrowIfCancellationRequested();

			if (checkersCompleted == checkersTcs.Task)
				checkersLine = await checkersTcs.Task.ConfigureAwait(false);

			// Parse FEN from captured lines
			string? rawFenCache = null;
			Fen.TryParseUciOutputLine(fenLine, ref rawFenCache, out var fenFromFen);

			if (checkersLine is null) return fenFromFen;

			// Enrich the cached FEN with 'checkers' info
			Fen.TryParseUciOutputLine(checkersLine, ref rawFenCache, out var fenWithCheckers);
			return fenWithCheckers;
		}
		finally
		{
			LineReceived -= Handler;
		}
	}

	/// <summary>
	/// Issues a "go perft 1" command and harvests all legal moves listed in the output.
	/// Waits for "readyok" for completion.
	/// </summary>
	public async Task<IReadOnlyCollection<string>> GetLegalMovesViaGoPerft1Async(CancellationToken ct)
	{
		// Collect moves while the engine processes the command; completion is gated by readyok.
		var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		LineReceived += CaptureMoves;
		try
		{
			await _transport.WriteLineAsync($"{UciConstants.Commands.GO_PERFT} 1", ct).ConfigureAwait(false);
			await IsReadyAsync(ct).ConfigureAwait(false);
		}
		finally
		{
			LineReceived -= CaptureMoves;
		}

		return results.ToList();

		static bool IsRank(char c) => (uint)(c - '1') <= 7; // 1-8

		static bool IsPromo(char c)
		{
			int lc = c | 0x20; // to lower ASCII
			return lc is 'q' or 'r' or 'b' or 'n';
		}

		static bool IsUciMove(ReadOnlySpan<char> s)
		{
			// length must be 4 or 5
			if ((uint)s.Length - 4 > 1) return false;

			if (!IsFile(s[0]) || !IsRank(s[1]) || !IsFile(s[2]) || !IsRank(s[3])) return false;

			return s.Length == 4 || IsPromo(s[4]);
		}

		static bool IsSep(char ch) =>
			ch is ' ' or '\t' or ',' or ';' or '|' or ':';

		/// <summary>
		/// Receives each output line and extracts UCI moves.
		/// </summary>
		void CaptureMoves(string l)
		{
			var span = l.AsSpan();
			var i    = 0;
			while (i < span.Length)
			{
				// skip separators
				while (i < span.Length && IsSep(span[i])) i++;

				int start = i;
				while (i < span.Length && !IsSep(span[i])) i++;

				int len = i - start;
				if ((uint)(len - 4) > 1) continue; // 4 or 5

				var tok = span.Slice(start, len);
				if (IsUciMove(tok)) results.Add(new(tok));
			}
		}

		static bool IsFile(char c) => (uint)((c | 0x20) - 'a') <= 7; // a-h
	}

	/// <summary>
	/// Runs a search with the supplied parameters and returns a <see cref="SearchResult"/> from "bestmove" and info lines.
	/// Applies a derived timeout based on input <paramref name="parameters"/>.
	/// </summary>
	public async Task<SearchResult> GoAsync(SearchParameters parameters, CancellationToken ct)
	{
		string cmd = BuildGoCommand(parameters);

		// Create and publish the active session before sending the command to avoid races.
		var session = new SearchSession(parameters.Ponder);
		_activeSearch = session;

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

		/// <summary>
		/// Computes an appropriate timeout for the search session, based on input search parameters.
		/// </summary>
		static TimeSpan ComputeTimeout(SearchParameters p)
		{
			if (p.MoveTimeMs is { } mt)
			{
				// add a small buffer to allow the engine to flush "bestmove"
				int buffered = Math.Clamp(mt + 750, 500, 60_000);
				return TimeSpan.FromMilliseconds(buffered);
			}

			if (p.Infinite) return TimeSpan.FromSeconds(120);
			if (p.Depth is not { } d) return TimeSpan.FromSeconds(p.Nodes is { } ? 60 : 30);

			int sec = Math.Clamp((int)d * 2, 10, 90);
			return TimeSpan.FromSeconds(sec);
		}
	}

	/// <summary>
	/// Disposes the engine client, releasing and stopping underlying transports.
	/// </summary>
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

	/// <summary>
	/// Main receive/read loop. Reads transport output lines, dispatches output events, parses known output, and
	/// manages generic waiters and search session events.
	/// </summary>
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

					if (line.StartsWith($"{UciConstants.Prefixes.INFO} ", StringComparison.OrdinalIgnoreCase))
					{
						if (PrincipalVariation.TryParse(line, out var pv))
							InfoPvReceived?.Invoke(pv);

						// Capture in active session (if any)
						sess?.Lines.Enqueue(line);
					}

					if (line.StartsWith($"{UciConstants.Prefixes.BEST_MOVE} ", StringComparison.OrdinalIgnoreCase))
					{
						// Zero-allocation parse of "bestmove <move> [ponder <move>]"
						string best   = string.Empty;
						string ponder = string.Empty;

						var s = line.AsSpan(9); // after "bestmove "
						var i = 0;

						// skip spaces, then read best move
						while (i < s.Length && s[i] == ' ') i++;
						int start = i;
						while (i < s.Length && s[i] != ' ') i++;
						if (i > start)
							best = new(s.Slice(start, i - start));

						// parse optional "ponder <move>"
						while (i < s.Length && s[i] == ' ') i++;
						if (i + 6 <= s.Length)
						{
							var rem = s.Slice(i);
							if (rem.Length >= 6 &&
								(rem[0] | 0x20) == 'p' &&
								(rem[1] | 0x20) == 'o' &&
								(rem[2] | 0x20) == 'n' &&
								(rem[3] | 0x20) == 'd' &&
								(rem[4] | 0x20) == 'e' &&
								(rem[5] | 0x20) == 'r')
							{
								i += 6;
								while (i < s.Length && s[i] == ' ') i++;
								int pstart = i;
								while (i < s.Length && s[i] != ' ') i++;
								if (i > pstart)
									ponder = new(s.Slice(pstart, i - pstart));
							}
						}

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
					if (Volatile.Read(ref _waiterCount) == 0) continue;

					foreach (var (key, value) in _waiters)
					{
						bool match;
						try
						{
							match = value.predicate(line);
						}
						catch
						{
							match = false;
						}

						if (!match) continue;
						if (!_waiters.TryRemove(key, out var removed)) continue;

						Interlocked.Decrement(ref _waiterCount);
						removed.tcs.TrySetResult(line);
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
			if (!_waiters.TryRemove(kv.Key, out var w)) continue;

			Interlocked.Decrement(ref _waiterCount);
			w.tcs.TrySetCanceled();
		}

		// Fail any active search session if the stream ends unexpectedly
		_activeSearch?.BestMoveTcs.TrySetCanceled();
		_activeSearch = null;

		// Ensure we are not stuck in a non-idle state if the read loop ends
		SetActivity(EngineActivity.Idle);
	}

	/// <summary>
	/// Awaits an output line matching a predicate within an optional timeout and cancellation.
	/// </summary>
	private async Task<string> WaitForLineAsync(Func<string, bool> predicate, TimeSpan timeout, CancellationToken ct)
	{
		var id  = Guid.NewGuid();
		var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
		if (_waiters.TryAdd(id, (predicate, tcs))) Interlocked.Increment(ref _waiterCount);

		// Fast path: no timeout and caller token cannot cancel -> avoid extra CTS/registration allocations.
		if (timeout == Timeout.InfiniteTimeSpan && !ct.CanBeCanceled)
			try
			{
				return await tcs.Task.ConfigureAwait(false);
			}
			finally
			{
				if (_waiters.TryRemove(id, out _)) Interlocked.Decrement(ref _waiterCount);
			}

		using var timeoutCts = timeout == Timeout.InfiniteTimeSpan ? null : new CancellationTokenSource(timeout);
		using var linked = timeoutCts is null
							   ? CancellationTokenSource.CreateLinkedTokenSource(ct)
							   : CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

		// ReSharper disable once UseAwaitUsing
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

	/// <summary>
	/// Atomically sets the engine activity state and publishes activity change notifications, if the state changed.
	/// </summary>
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

	/// <summary>
	/// Holds shared data for a search session for the duration of an active "go ..." command.
	/// Tracks output lines and the bestmove completion.
	/// </summary>
	private sealed class SearchSession(bool ponder)
	{
		/// <summary>True if this session was started in pondering mode.</summary>
		public readonly bool                    Ponder = ponder;
		/// <summary>Collects all info/bestmove lines output during the search.</summary>
		public readonly ConcurrentQueue<string> Lines  = new();
		/// <summary>Completes when the engine emits "bestmove".</summary>
		public readonly TaskCompletionSource<string> BestMoveTcs =
			new(TaskCreationOptions.RunContinuationsAsynchronously);
	}
}

/// <summary>
/// Enumerates the coarse-grained activity states tracked for the engine.
/// </summary>
public enum EngineActivity
{
	/// <summary>Idle; engine is not searching or pondering.</summary>
	Idle      = 0,
	/// <summary>Engine is actively searching.</summary>
	Searching = 1,
	/// <summary>Engine is pondering (analyzing at opponent's turn).</summary>
	Pondering = 2
}
