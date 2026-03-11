using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.UCI.API.Types;
using Bezoro.UCI.Domain.EngineClient;

namespace Bezoro.UCI.Domain;

/// <summary>
///     Provides high-level async orchestration and lifecycle management for a UCI engine process/transport.
///     Maintains a single search session at a time, parses UCI output, tracks engine activity state, and
///     exposes events and helpers for UCI clients.
/// </summary>
internal sealed class UciEngineClient : IAsyncDisposable, IUciLineSource
{
	private readonly EngineActivityTracker _activityTracker = new();
	/// <summary>
	///     Underlying transport abstraction for communicating with the engine.
	/// </summary>
	private readonly IUciTransport _transport;
	private readonly object                 _lifecycleLock = new();
	private readonly UciEngineCommandModule _commandModule;

	/// <summary>
	///     Registry for awaiting specific engine output lines.
	/// </summary>
	private readonly UciLineWaiterRegistry _lineWaiters = new();
	private readonly UciOutputDispatcher _outputDispatcher;

	private readonly UciSearchCoordinator _searchCoordinator;

	/// <summary>
	///     Cancellation source for the read loop and engine lifetime.
	/// </summary>
	private CancellationTokenSource? _cts;

	private Task? _readerTask;

	/// <summary>
	///     Occurs when the engine transitions between <see cref="EngineActivity" /> states.
	/// </summary>
	public event Action<EngineActivity, EngineActivity>? ActivityChanged
	{
		add => _activityTracker.ActivityChanged += value;
		remove => _activityTracker.ActivityChanged -= value;
	}

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

	public UciEngineClient(IUciTransport transport)
	{
		_transport = transport ?? throw new ArgumentNullException(nameof(transport));

		_searchCoordinator = new(
			_transport,
			SetActivity,
			PublishInfoPvSafe,
			PublishBestMoveSafe
		);

		_outputDispatcher = new(_lineWaiters, _searchCoordinator);
		_commandModule    = new(_transport, _lineWaiters, this, SetActivity);
	}

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
	public EngineActivity Activity => _activityTracker.Current;

	/// <summary>
	///     Engine process/transport status.
	/// </summary>
	public TransportStatus Status => _transport.Status;

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
	public Task GoFireAndForgetAsync(SearchParameters parameters, CancellationToken ct) =>
		_commandModule.GoFireAndForgetAsync(parameters, ct);

	/// <summary>
	///     Sends "isready" to the engine and waits up to 10 seconds for "readyok".
	/// </summary>
	public Task IsReadyAsync(CancellationToken ct) => _commandModule.IsReadyAsync(ct);

	/// <summary>
	///     Sends a "setoption" command to the engine.
	/// </summary>
	public Task SetOptionAsync(string name, string? value, CancellationToken ct) =>
		_commandModule.SetOptionAsync(name, value, ct);

	/// <summary>
	///     Sets the board position using a FEN and (optionally) a move list.
	/// </summary>
	public Task SetPositionAsync(Fen fen, IEnumerable<string>? moves, CancellationToken ct) =>
		_commandModule.SetPositionAsync(fen, moves, ct);

	/// <summary>
	///     Starts the engine process/connection, background read loop, and completes UCI handshake.
	/// </summary>
	public async Task StartAsync(CancellationToken ct = default)
	{
		lock (_lifecycleLock)
		{
			if (_cts is { })
				throw new InvalidOperationException("Engine client is already started.");

			_cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
		}

		try
		{
			await _transport.StartAsync(ct).ConfigureAwait(false);

			lock (_lifecycleLock)
			{
				// ReSharper disable once MethodSupportsCancellation
				_readerTask = Task.Run(ReadLoopAsync);
			}

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
		catch
		{
			lock (_lifecycleLock)
			{
				_cts?.Dispose();
				_cts        = null;
				_readerTask = null;
			}

			throw;
		}
	}

	/// <summary>
	///     Gracefully stops the engine, read loop, and disposes transports.
	/// </summary>
	public async Task StopAsync(CancellationToken ct = default)
	{
		CancellationTokenSource? ctsToCancel       = null;
		Task?                    readerTaskToAwait = null;

		lock (_lifecycleLock)
		{
			if (_cts is null) return; // Already stopped

			ctsToCancel       = _cts;
			readerTaskToAwait = _readerTask;
			_cts              = null;
			_readerTask       = null;
		}

		try
		{
			ctsToCancel.Cancel();
			if (readerTaskToAwait is { })
				try
				{
					await readerTaskToAwait.ConfigureAwait(false);
				}
				catch
				{
					/* best-effort */
				}
		}
		finally
		{
			ctsToCancel.Dispose();
		}

		await _transport.StopAsync(ct).ConfigureAwait(false);
		SetActivity(EngineActivity.Idle);
	}

	/// <summary>
	///     Sends the "stop" command to the engine and immediately transitions to Idle.
	/// </summary>
	public Task StopSearchAsync(CancellationToken ct) => _commandModule.StopSearchAsync(ct);

	/// <summary>
	///     Sends "uci" to the engine and waits for "uciok" and "readyok" to confirm supported handshaking.
	/// </summary>
	public Task UciInitAsync(CancellationToken ct) => _commandModule.UciInitAsync(ct);

	/// <summary>
	///     Informs the engine of a new game context via "ucinewgame"; calls <see cref="IsReadyAsync" />.
	/// </summary>
	public Task UciNewGameAsync(CancellationToken ct) => _commandModule.UciNewGameAsync(ct);

	/// <summary>
	///     Requests the current engine FEN using the "d" command and parses "fen ..." and "checkers ..." output.
	/// </summary>
	public Task<Fen?> GetFenViaDAsync(CancellationToken ct) => _commandModule.GetFenViaDAsync(ct);

	/// <summary>
	///     Issues a "go perft 1" command and harvests all legal moves listed in the output.
	///     Waits for "readyok" for completion.
	/// </summary>
	public Task<IReadOnlyCollection<string>> GetLegalMovesViaGoPerft1Async(CancellationToken ct) =>
		_commandModule.GetLegalMovesViaGoPerft1Async(ct);

	/// <summary>
	///     Runs a search with the supplied parameters and returns a <see cref="SearchResult" /> from "bestmove" and info
	///     lines.
	///     Applies a derived timeout based on input <paramref name="parameters" />.
	/// </summary>
	public Task<SearchResult> GoAsync(SearchParameters parameters, CancellationToken ct) =>
		_searchCoordinator.ExecuteSearchAsync(parameters, ct);

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

		await _transport.DisposeAsync();
	}

	IDisposable IUciLineSource.Subscribe(Action<string> handler)
	{
		if (handler is null) throw new ArgumentNullException(nameof(handler));

		LineReceived += handler;
		return new EventSubscription(() => LineReceived -= handler);
	}

	/// <summary>
	///     Main receive/read loop. Reads transport output lines, dispatches output events, parses known output, and
	///     manages generic waiters and search session events.
	/// </summary>
	private async Task ReadLoopAsync()
	{
		CancellationToken token;
		lock (_lifecycleLock)
		{
			if (_cts is null)
			{
				// Client was stopped before this task started - exit immediately
				_outputDispatcher.OnShutdown();
				SetActivity(EngineActivity.Idle);
				return;
			}

			token = _cts.Token;
		}

		try
		{
			await foreach (string line in _transport.ReadLinesAsync(token).ConfigureAwait(false))
			{
				try
				{
					LineReceived?.Invoke(line);
				}
				catch
				{
					// External subscribers must not interfere with protocol processing.
				}

				try
				{
					_outputDispatcher.Process(line);
				}
				catch
				{
					// Keep loop alive if output dispatch hits an unexpected failure.
				}
			}
		}
		catch
		{
			// gracefully end
		}
		finally
		{
			_outputDispatcher.OnShutdown();
			SetActivity(EngineActivity.Idle);
		}
	}

	/// <summary>
	///     Atomically sets the engine activity state and publishes activity change notifications, if the state changed.
	/// </summary>
	private void SetActivity(EngineActivity next)
	{
		_activityTracker.Set(next);
	}

	private void PublishBestMoveSafe(string bestMove, string ponderMove)
	{
		try
		{
			BestMoveReceived?.Invoke(bestMove, ponderMove);
		}
		catch
		{
			// External subscribers must not interfere with search completion.
		}
	}

	private void PublishInfoPvSafe(PrincipalVariation pv)
	{
		try
		{
			InfoPvReceived?.Invoke(pv);
		}
		catch
		{
			// External subscribers must not interfere with PV capture.
		}
	}

	private sealed class EventSubscription(Action unsubscribe) : IDisposable
	{
		private readonly Action _unsubscribe = unsubscribe ?? throw new ArgumentNullException(nameof(unsubscribe));
		private          int    _disposed;

		public void Dispose()
		{
			if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

			try
			{
				_unsubscribe();
			}
			catch
			{
				/* swallow */
			}
		}
	}
}
