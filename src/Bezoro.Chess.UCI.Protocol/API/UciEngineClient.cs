using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.Chess.UCI.Protocol.Domain.Common.Constants;
using Bezoro.Chess.UCI.Protocol.Domain.EngineClient;

namespace Bezoro.Chess.UCI.Protocol.API;

/// <summary>
///     Provides high-level async orchestration and lifecycle management for a UCI engine process/transport.
///     Maintains a single search session at a time, parses UCI output, tracks engine activity state, and
///     exposes events and helpers for UCI clients.
/// </summary>
public sealed class UciEngineClient : IAsyncDisposable, IUciLineSource
{
	private readonly EngineActivityTracker _activityTracker = new();
	/// <summary>
	///     Underlying transport abstraction for communicating with the engine.
	/// </summary>
	private readonly IUciTransport _transport;
	private readonly object                 _lifecycleLock = new();
	private readonly object                 _metadataLock  = new();
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
	private ImmutableArray<UciEngineOption> _availableOptions = ImmutableArray<UciEngineOption>.Empty;

	private Task?                 _readerTask;
	private UciEngineCapabilities _capabilities = UciEngineCapabilities.Unknown;
	private UciEngineInfo         _engineInfo   = UciEngineInfo.Empty;

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
	///     Raised when the transport or client internals observe an unexpected protocol-processing failure.
	/// </summary>
	public event Action<Exception>? Error;

	/// <summary>
	///     Raised for principal variation ("info ... pv ...") lines.
	/// </summary>
	public event Action<PrincipalVariation>? InfoPvReceived;

	/// <summary>
	///     Raised for every output line received from the engine.
	/// </summary>
	public event Action<string>? LineReceived;

	/// <summary>
	///     Initializes a new client backed by a process transport for the supplied engine executable.
	/// </summary>
	/// <param name="enginePath">Path to the engine executable.</param>
	/// <param name="args">Optional process arguments passed to the engine.</param>
	/// <param name="workingDirectory">Optional working directory used when starting the engine process.</param>
	public UciEngineClient(string enginePath, IEnumerable<string>? args = null, string? workingDirectory = null)
		: this(new ProcessUciTransport(enginePath, args, workingDirectory)) { }

	internal UciEngineClient(IUciTransport transport)
	{
		_transport       =  transport ?? throw new ArgumentNullException(nameof(transport));
		_transport.Error += OnTransportError;

		_searchCoordinator = new(
			_transport,
			SetActivity,
			PublishInfoPvSafe,
			PublishBestMoveSafe
		);

		_outputDispatcher = new(_lineWaiters, _searchCoordinator, ObserveProtocolLine);
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
	///     Gets the options advertised by the engine during handshake.
	/// </summary>
	public IReadOnlyList<UciEngineOption> AvailableOptions
	{
		get
		{
			lock (_metadataLock)
			{
				return _availableOptions;
			}
		}
	}

	/// <summary>
	///     Engine process/transport status.
	/// </summary>
	public TransportStatus Status => _transport.Status;

	/// <summary>
	///     Gets the standard and extension capabilities discovered for the current engine.
	/// </summary>
	public UciEngineCapabilities Capabilities
	{
		get
		{
			lock (_metadataLock)
			{
				return _capabilities;
			}
		}
	}

	/// <summary>
	///     Gets the engine metadata captured from <c>id name</c> and <c>id author</c> output.
	/// </summary>
	public UciEngineInfo EngineInfo
	{
		get
		{
			lock (_metadataLock)
			{
				return _engineInfo;
			}
		}
	}

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
	///     Sends the standard UCI <c>ponderhit</c> command.
	/// </summary>
	public Task PonderHitAsync(CancellationToken ct) => _commandModule.PonderHitAsync(ct);

	/// <summary>
	///     Sends the standard UCI <c>register</c> command.
	/// </summary>
	public Task RegisterAsync(UciRegistration registration, CancellationToken ct) =>
		_commandModule.RegisterAsync(registration, ct);

	/// <summary>
	///     Sends the standard UCI <c>debug on/off</c> command.
	/// </summary>
	public Task SetDebugAsync(bool enabled, CancellationToken ct) => _commandModule.SetDebugAsync(enabled, ct);

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
	public async Task UciInitAsync(CancellationToken ct)
	{
		ResetHandshakeMetadata();
		await _commandModule.UciInitAsync(ct).ConfigureAwait(false);
		PromoteStandardCapabilities();
	}

	/// <summary>
	///     Informs the engine of a new game context via "ucinewgame"; calls <see cref="IsReadyAsync" />.
	/// </summary>
	public Task UciNewGameAsync(CancellationToken ct) => _commandModule.UciNewGameAsync(ct);

	/// <summary>
	///     Requests the current engine FEN using the non-standard <c>d</c> command and parses
	///     <c>fen ...</c> and <c>checkers ...</c> output.
	/// </summary>
	public Task<Fen?> TryGetFenViaDisplayBoardAsync(CancellationToken ct) =>
		_commandModule.TryGetFenViaDisplayBoardAsync(ct);

	/// <summary>
	///     Issues the non-standard <c>go perft 1</c> command and harvests all legal moves listed in the output.
	///     Waits for "readyok" for completion.
	/// </summary>
	public Task<IReadOnlyCollection<string>> GetLegalMovesViaPerftAsync(CancellationToken ct) =>
		_commandModule.GetLegalMovesViaPerftAsync(ct);

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
		_transport.Error -= OnTransportError;

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

	internal void SetExtensionCapabilities(
		UciCapabilityState displayBoardFen,
		UciCapabilityState perftMoveListing)
	{
		lock (_metadataLock)
		{
			_capabilities = _capabilities with
			{
				DisplayBoardFen = displayBoardFen,
				PerftMoveListing = perftMoveListing
			};
		}
	}

	IDisposable IUciLineSource.Subscribe(Action<string> handler)
	{
		if (handler is null) throw new ArgumentNullException(nameof(handler));

		LineReceived += handler;
		return new EventSubscription(() => LineReceived -= handler);
	}

	private static bool TryParseIdLine(string line, string idToken, out string value)
	{
		var prefix = $"{UciConstants.Prefixes.ID} {idToken} ";
		if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
		{
			value = line[prefix.Length..].Trim();
			return true;
		}

		value = string.Empty;
		return false;
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
				catch (Exception ex)
				{
					ReportInternalError(ex);
				}
			}
		}
		catch (OperationCanceledException) when (token.IsCancellationRequested)
		{
			// Graceful shutdown.
		}
		catch (Exception ex)
		{
			ReportInternalError(ex);
		}
		finally
		{
			_outputDispatcher.OnShutdown();
			SetActivity(EngineActivity.Idle);
		}
	}

	private void ObserveProtocolLine(string line)
	{
		if (string.IsNullOrWhiteSpace(line)) return;

		if (TryParseIdLine(line, UciConstants.Keywords.NAME, out string name))
		{
			lock (_metadataLock)
			{
				_engineInfo = _engineInfo with { Name = name };
			}

			return;
		}

		if (TryParseIdLine(line, UciConstants.Keywords.AUTHOR, out string author))
		{
			lock (_metadataLock)
			{
				_engineInfo = _engineInfo with { Author = author };
			}

			return;
		}

		if (!UciEngineOption.TryParse(line, out var option)) return;

		lock (_metadataLock)
		{
			var builder = _availableOptions.ToBuilder();
			int index   = -1;
			for (var i = 0; i < builder.Count; i++)
			{
				if (!string.Equals(builder[i].Name, option.Name, StringComparison.OrdinalIgnoreCase)) continue;

				index = i;
				break;
			}

			if (index >= 0)
				builder[index] = option;
			else
				builder.Add(option);

			_availableOptions = builder.ToImmutable();
			PromoteStandardCapabilities_NoLock();
		}
	}

	private void OnTransportError(Exception ex) => ReportInternalError(ex);

	private void PromoteStandardCapabilities()
	{
		lock (_metadataLock)
		{
			PromoteStandardCapabilities_NoLock();
		}
	}

	private void PromoteStandardCapabilities_NoLock()
	{
		bool supportsPonder = _availableOptions.Any(static option =>
														string.Equals(
															option.Name, "Ponder", StringComparison.OrdinalIgnoreCase
														)
		);

		_capabilities = _capabilities with
		{
			DebugCommand = UciCapabilityState.Supported,
			RegisterCommand = UciCapabilityState.Supported,
			PonderHit = supportsPonder ? UciCapabilityState.Supported : UciCapabilityState.Unknown
		};
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

	private void ReportInternalError(Exception ex)
	{
		try
		{
			Error?.Invoke(ex);
		}
		catch
		{
			// External subscribers must not interfere with client teardown or protocol handling.
		}
	}

	private void ResetHandshakeMetadata()
	{
		lock (_metadataLock)
		{
			_engineInfo       = UciEngineInfo.Empty;
			_availableOptions = ImmutableArray<UciEngineOption>.Empty;
			_capabilities     = UciEngineCapabilities.Unknown;
		}
	}

	/// <summary>
	///     Atomically sets the engine activity state and publishes activity change notifications, if the state changed.
	/// </summary>
	private void SetActivity(EngineActivity next)
	{
		_activityTracker.Set(next);
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
