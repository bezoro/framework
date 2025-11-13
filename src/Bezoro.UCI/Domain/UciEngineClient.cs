using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.UCI.API.Types;
using Bezoro.UCI.Domain.Common.Constants;

namespace Bezoro.UCI.Domain;

/// <summary>
///     Provides high-level async orchestration and lifecycle management for a UCI engine process/transport.
///     Maintains a single search session at a time, parses UCI output, tracks engine activity state, and
///     exposes events and helpers for UCI clients.
/// </summary>
internal sealed class UciEngineClient(IUciTransport transport) : IAsyncDisposable
{
	/// <summary>
	///     Underlying transport abstraction for communicating with the engine.
	/// </summary>
	private readonly IUciTransport _transport = transport ?? throw new ArgumentNullException(nameof(transport));

	/// <summary>
	///     Registry for awaiting specific engine output lines.
	/// </summary>
	private readonly UciLineWaiterRegistry _lineWaiters = new();

	/// <summary>
	///     Cancellation source for the read loop and engine lifetime.
	/// </summary>
	private CancellationTokenSource? _cts;

	/// <summary>
	///     Atomic int representing current engine activity state (see <see cref="EngineActivity" />).
	/// </summary>
	private int _activity;

	/// <summary>
	///     Current <see cref="SearchSession" /> tracked for an ongoing search operation.
	///     Only one search is permitted at a time.
	/// </summary>
	private volatile SearchSession? _activeSearch;

	/// <summary>
	///     Holds background read loop Task.
	/// </summary>
	private Task? _readerTask;

	/// <summary>
	///     Occurs when the engine transitions between <see cref="EngineActivity" /> states.
	/// </summary>
	public event Action<EngineActivity, EngineActivity>? ActivityChanged;

	/// <summary>
	///     Notifies when the engine emits a "bestmove" line.
	/// </summary>
	public event Action<string, string>? BestMoveReceived;

	/// <summary>
	///     Raised for principal variation ("info ... pv ...") lines.
	/// </summary>
	public event Action<PrincipalVariation>? InfoPvReceived;

	/// <summary>
	///     Raised for every output line received from the engine.
	/// </summary>
	public event Action<string>? LineReceived;

	/// <summary>
	///     Indicates whether the underlying transport is healthy.
	/// </summary>
	public bool IsHealthy => _transport.IsHealthy;

	/// <summary>
	///     Returns true after <see cref="StartAsync" /> is called and until <see cref="StopAsync" /> completes.
	/// </summary>
	public bool IsStarted => _transport.IsStarted;

	/// <summary>
	///     Gets the current engine activity state.
	/// </summary>
	public EngineActivity Activity => (EngineActivity)Volatile.Read(ref _activity);

	/// <summary>
	///     Engine process/transport status.
	/// </summary>
	public ProcessUciTransport.TransportStatus Status => _transport.Status;

	/// <summary>
	///     Determines if a string is a valid UCI move (e.g. "e2e4", "a7a8q").
	/// </summary>
	public static bool IsUciMoveString(string s) => UciCommandBuilder.IsUciMoveString(s);

	/// <summary>
	///     Builds a UCI-compliant "go ..." command from <paramref name="parameters" />.
	/// </summary>
	/// <param name="parameters">Search configuration</param>
	/// <returns>Full "go ..." line to send to engine</returns>
	public static string BuildGoCommand(SearchParameters parameters) =>
		UciCommandBuilder.BuildGoCommand(parameters);

	/// <summary>
	///     Starts a search with the given parameters and does not await engine termination nor the bestmove response.
	///     Use for fire-and-forget searches (e.g., GUI spinning).
	/// </summary>
	public async Task GoFireAndForgetAsync(SearchParameters parameters, CancellationToken ct)
	{
		string cmd = BuildGoCommand(parameters);
		await _transport.WriteLineAsync(cmd, ct).ConfigureAwait(false);
		SetActivity(parameters.Ponder ? EngineActivity.Pondering : EngineActivity.Searching);
	}

	/// <summary>
	///     Sends "isready" to the engine and waits up to 10 seconds for "readyok".
	/// </summary>
	public async Task IsReadyAsync(CancellationToken ct)
	{
		await _transport.WriteLineAsync(UciConstants.Commands.IS_READY, ct).ConfigureAwait(false);
		await _lineWaiters.WaitForAsync(
							  static l => l.Trim().Equals(
								  UciConstants.Responses.READY_OK,
								  StringComparison.OrdinalIgnoreCase),
							  TimeSpan.FromSeconds(10),
							  ct)
						  .ConfigureAwait(false);
	}

	/// <summary>
	///     Sends a "setoption" command to the engine.
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
	///     Sets the board position using a FEN and (optionally) a move list.
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
	///     Starts the engine process/connection, background read loop, and completes UCI handshake.
	/// </summary>
	public async Task StartAsync(CancellationToken ct = default)
	{
		await _transport.StartAsync(ct).ConfigureAwait(false);

		_cts = new();
		// ReSharper disable once MethodSupportsCancellation
		_readerTask = Task.Run(ReadLoopAsync);

		try
		{
			await UciInitAsync(ct).ConfigureAwait(false);
			SetActivity(EngineActivity.Idle);
		}
		catch
		{
			try
			{
				await StopAsync(CancellationToken.None).ConfigureAwait(false);
			}
			catch
			{
				/* best-effort cleanup */
			}

			throw;
		}
	}

	/// <summary>
	///     Gracefully stops the engine, read loop, and disposes transports.
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
	///     Sends the "stop" command to the engine and immediately transitions to Idle.
	/// </summary>
	public async Task StopSearchAsync(CancellationToken ct)
	{
		await _transport.WriteLineAsync(UciConstants.Commands.STOP, ct).ConfigureAwait(false);
		SetActivity(EngineActivity.Idle);
	}

	/// <summary>
	///     Sends "uci" to the engine and waits for "uciok" and "readyok" to confirm supported handshaking.
	/// </summary>
	public async Task UciInitAsync(CancellationToken ct)
	{
		await _transport.WriteLineAsync(UciConstants.Commands.UCI, ct).ConfigureAwait(false);
		await _lineWaiters.WaitForAsync(
							  static l => l.Trim().Equals(
								  UciConstants.Responses.UCI_OK,
								  StringComparison.OrdinalIgnoreCase),
							  TimeSpan.FromSeconds(5),
							  ct)
						  .ConfigureAwait(false);

		await IsReadyAsync(ct).ConfigureAwait(false);
	}

	/// <summary>
	///     Informs the engine of a new game context via "ucinewgame"; calls <see cref="IsReadyAsync" />.
	/// </summary>
	public async Task UciNewGameAsync(CancellationToken ct)
	{
		await _transport.WriteLineAsync(UciConstants.Commands.UCI_NEW_GAME, ct).ConfigureAwait(false);
		await IsReadyAsync(ct).ConfigureAwait(false);
	}

	/// <summary>
	///     Requests the current engine FEN using the "d" command and parses "fen ..." and "checkers ..." output.
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
			var fenCompleted = await Task.WhenAny(fenTcs.Task, Task.Delay(TimeSpan.FromSeconds(2), ct))
										 .ConfigureAwait(false);

			ct.ThrowIfCancellationRequested();

			if (fenCompleted == fenTcs.Task)
				fenLine = await fenTcs.Task.ConfigureAwait(false);

			if (fenLine is null)
				return null;

			// Give a short window for the 'checkers' line to arrive after the FEN line
			string? checkersLine = null;
			var checkersCompleted = await Task.WhenAny(
												  checkersTcs.Task,
												  Task.Delay(TimeSpan.FromMilliseconds(750), ct))
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
	///     Issues a "go perft 1" command and harvests all legal moves listed in the output.
	///     Waits for "readyok" for completion.
	/// </summary>
	public async Task<IReadOnlyCollection<string>> GetLegalMovesViaGoPerft1Async(CancellationToken ct)
	{
		var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		void CaptureMoves(string line) => UciCommandBuilder.CollectMovesFromLine(line, results);

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
	}

	/// <summary>
	///     Runs a search with the supplied parameters and returns a <see cref="SearchResult" /> from "bestmove" and info
	///     lines.
	///     Applies a derived timeout based on input <paramref name="parameters" />.
	/// </summary>
	public async Task<SearchResult> GoAsync(SearchParameters parameters, CancellationToken ct)
	{
		string cmd = BuildGoCommand(parameters);

		// Create and publish the active session before sending the command to avoid races.
		var session = new SearchSession(parameters.Ponder);
		if (Interlocked.CompareExchange(ref _activeSearch, session, null) is { })
		{
			session.BestMoveCompletion.TrySetCanceled();
			throw new InvalidOperationException("A search is already in progress.");
		}

		var timeout = ComputeTimeout(parameters);

		string? bestLine = null;
		// Await bestmove with timeout and cancellation without mutating the session TCS state.
		try
		{
			await _transport.WriteLineAsync(cmd, ct).ConfigureAwait(false);
			SetActivity(parameters.Ponder ? EngineActivity.Pondering : EngineActivity.Searching);

			var bestTask = session.BestMoveCompletion.Task;
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
					await _transport.WriteLineAsync(UciConstants.Commands.STOP, CancellationToken.None)
									.ConfigureAwait(false);
				}
				catch
				{
					/* best-effort */
				}

				try
				{
					var graceCompleted = await Task.WhenAny(bestTask, Task.Delay(TimeSpan.FromSeconds(2), ct))
												   .ConfigureAwait(false);

					ct.ThrowIfCancellationRequested();

					if (graceCompleted == bestTask)
					{
						bestLine = await bestTask.ConfigureAwait(false);
					}
					else
					{
						// Still no bestmove; if we captured one already, use it.
						bestLine = session.FindFirstLine(static l => l.StartsWith(
															 UciConstants.Prefixes.BEST_MOVE + " ",
															 StringComparison.OrdinalIgnoreCase));

						if (bestLine is null)
							throw new TimeoutException("Engine search timed out without emitting 'bestmove'.");
					}
				}
				catch (OperationCanceledException) when (!ct.IsCancellationRequested)
				{
					// Treat as timeout in this context.
					bestLine = session.FindFirstLine(static l => l.StartsWith(
														 UciConstants.Prefixes.BEST_MOVE + " ",
														 StringComparison.OrdinalIgnoreCase));

					if (bestLine is null) throw;
				}
			}
		}
		finally
		{
			if (ReferenceEquals(_activeSearch, session))
				_activeSearch = null;

			if (!session.BestMoveCompletion.Task.IsCompleted)
				session.BestMoveCompletion.TrySetCanceled();

			// Ensure we flip to Idle; read loop will also do so on bestmove.
			SetActivity(EngineActivity.Idle);
		}

		// Ensure bestmove line is present in the buffer for parsing
		if (bestLine is { })
			session.AddLine(bestLine);

		// Snapshot captured lines once; SearchResult.TryParse expects a stable enumerable
		var snapshot = session.SnapshotLines();

		return SearchResult.TryParse(snapshot, out var result) ? result : default;
	}

	/// <summary>
	///     Disposes the engine client, releasing and stopping underlying transports.
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
	///     Computes an appropriate timeout for the search session, based on input search parameters.
	/// </summary>
	private static TimeSpan ComputeTimeout(SearchParameters p)
	{
		if (p.MoveTimeMs is { } mt)
		{
			// add a small buffer to allow the engine to flush "bestmove"
			long buffered                = (long)mt + 750;
			if (buffered < 500) buffered = 500;

			double capped = Math.Min(buffered, TimeSpan.MaxValue.TotalMilliseconds);
			return TimeSpan.FromMilliseconds(capped);
		}

		if (p.Infinite) return TimeSpan.FromSeconds(120);
		if (p.Depth is not { } d) return TimeSpan.FromSeconds(p.Nodes is { } ? 60 : 30);

		long seconds = Math.Clamp((long)d * 2, 10L, 90L);
		return TimeSpan.FromSeconds(seconds);
	}

	/// <summary>
	///     Main receive/read loop. Reads transport output lines, dispatches output events, parses known output, and
	///     manages generic waiters and search session events.
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
					ProcessTransportLine(line);
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
		finally
		{
			_lineWaiters.CancelAll();

			_activeSearch?.BestMoveCompletion.TrySetCanceled();
			_activeSearch = null;

			SetActivity(EngineActivity.Idle);
		}
	}

	private void HandleBestMoveLine(string line, SearchSession? session)
	{
		if (session is { })
		{
			session.AddLine(line);
			session.CompleteBestMove(line);
			_activeSearch = null;
		}

		SetActivity(EngineActivity.Idle);

		if (BestMoveLine.TryParse(line, out var bestMove))
		{
			BestMoveReceived?.Invoke(bestMove.BestMove, bestMove.PonderMove ?? string.Empty);
			return;
		}

		BestMoveReceived?.Invoke(string.Empty, string.Empty);
	}

	private void HandleInfoLine(string line, SearchSession? session)
	{
		if (PrincipalVariation.TryParse(line, out var pv))
			InfoPvReceived?.Invoke(pv);

		session?.AddLine(line);
	}

	private void ProcessTransportLine(string line)
	{
		LineReceived?.Invoke(line);

		var session = _activeSearch;

		if (line.StartsWith($"{UciConstants.Prefixes.INFO} ", StringComparison.OrdinalIgnoreCase))
			HandleInfoLine(line, session);
		else if (line.StartsWith($"{UciConstants.Prefixes.BEST_MOVE} ", StringComparison.OrdinalIgnoreCase))
			HandleBestMoveLine(line, session);

		_lineWaiters.Notify(line);
	}

	/// <summary>
	///     Atomically sets the engine activity state and publishes activity change notifications, if the state changed.
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
}

/// <summary>
///     Enumerates the coarse-grained activity states tracked for the engine.
/// </summary>
public enum EngineActivity
{
	/// <summary>Idle; engine is not searching or pondering.</summary>
	Idle = 0,
	/// <summary>Engine is actively searching.</summary>
	Searching = 1,
	/// <summary>Engine is pondering (analyzing at opponent's turn).</summary>
	Pondering = 2
}
