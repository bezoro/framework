using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Bezoro.Chess.UCI.API.Common.Enums;
using Bezoro.Chess.UCI.API.Types;
using Bezoro.Chess.UCI.Internal;
using Bezoro.Chess.UCI.Protocol.API.Common.Extensions;
using Bezoro.Chess.UCI.Protocol.API.Types;
using Bezoro.Chess.UCI.Protocol.Internal;

namespace Bezoro.Chess.UCI.API;

/// <summary>
///     Coordinates multiple UCI engines for fast information, pondering, and full move classification.
///     Orchestrates updating positions, synchronized pondering, background classification, and emits unified events
///     updating UI or consumers.
/// </summary>
public sealed class UciGameEngineSession : IAsyncDisposable, IDisposable
{
	private readonly SerializedUciEngineClientRuntime _classificationClient;
	private readonly CoordinatorClassificationRuntime _classificationRuntime;
	private readonly PlayableMatchClaimableDrawPolicy _claimableDrawPolicy;
	private readonly PlayableMatchControlledMoveFallbackPolicy _controlledMoveFallbackPolicy;
	private readonly PlayableMatchDrawOfferPolicy _drawOfferPolicy;
	private readonly GameEngineEventDispatcher        _events;
	private readonly MatchSideControllerKind          _blackController;
	private readonly List<ClockCheckpoint>            _clockHistory = [];
	private readonly char                             _perspectiveColor;
	private readonly object                           _sync = new();
	private readonly UciPonderRuntime                 _ponder;
	private readonly SerializedUciEngineClientRuntime _snapshotClient;
	private readonly PlayableMatchTimeControl?        _timeControl;
	private readonly MatchSideControllerKind          _whiteController;

	private readonly UciCoordinatorOptions _options;

	private CancellationTokenSource? _bestCts;
	private CancellationTokenSource? _classificationCts;
	private int                      _acceptedPonderGeneration;
	private Guid                     _gameId = Guid.NewGuid();
	private long                     _nextMoveId;
	private long                     _nextPendingPromotionId;
	private ImmutableArray<GameMoveEvent> _appliedMoveHistory = [];
	private PlayableMatchResult      _forcedResult;
	private PlayableMatchResult?     _claimableResult;
	private char?                    _drawOfferedBy;
	private bool                     _isClockPaused;
	private PlayableMatchResult      _lastResult;
	private CoordinatorPendingPromotionRequest? _pendingPromotion;

	private UciState _state = UciState.Default;

	/// <summary>
	///     Raised when an error occurs in any of the underlying engines.
	/// </summary>
	public event Action<Exception>? Error;

	/// <summary>
	///     Raised when the coordinator is fully initialized and ready.
	/// </summary>
	public event Action? Ready;

	/// <summary>
	///     Raised when a new gameplay session starts.
	/// </summary>
	public event Action<GameStartedEvent>? GameStarted;

	/// <summary>
	///     Raised when the coordinator state changes.
	/// </summary>
	public event Action<UciState>? StateChanged;

	/// <summary>
	///     Raised when the visible game position snapshot changes, including legal moves and terminal-state transitions.
	/// </summary>
	public event Action<UciState>? PositionChanged;

	/// <summary>
	///     Raised when a full position is loaded directly into the coordinator.
	/// </summary>
	public event Action<PositionLoadedEvent>? PositionLoaded;

	/// <summary>
	///     Raised when the legal move set for the visible position changes.
	/// </summary>
	public event Action<UciState>? LegalMovesUpdated;

	/// <summary>
	///     Raised when the search lifecycle toggles between searching and idle.
	/// </summary>
	public event Action<UciState>? SearchStateChanged;

	/// <summary>
	///     Raised when the ponder engine publishes a new principal variation.
	/// </summary>
	public event Action<PrincipalVariation>? EvaluationChanged;

	/// <summary>
	///     Obsolete alias for <see cref="EvaluationChanged"/>.
	/// </summary>
	[Obsolete("Use EvaluationChanged or EventPublished instead.")]
	public event Action<PrincipalVariation>? EvaluationUpdated;

	/// <summary>
	///     Raised when the ponder engine produces a new best-move pair for the current search.
	/// </summary>
	public event Action<UciState>? BestMoveChanged;

	/// <summary>
	///     Raised for each move classification produced for the current position.
	/// </summary>
	public event Action<Move>? MoveClassified;

	/// <summary>
	///     Obsolete alias for <see cref="MoveClassified"/>.
	/// </summary>
	[Obsolete("Use MoveClassified or EventPublished instead.")]
	public event Action<Move>? MoveClassificationUpdated;

	/// <summary>
	///     Raised when all legal moves for the current position have been classified.
	/// </summary>
	public event Action<UciState>? ClassificationCompleted;

	/// <summary>
	///     Raised when the current visible position is terminal and no legal moves remain.
	/// </summary>
	public event Action<UciState>? GameOver;

	/// <summary>
	///     Raised when the match result changes.
	/// </summary>
	public event Action<UciState>? ResultChanged;

	/// <summary>
	///     Raised when a move is fully applied.
	/// </summary>
	public event Action<GameMoveEvent>? MoveMade;

	/// <summary>
	///     Raised when an applied move captures a piece.
	/// </summary>
	public event Action<GameMoveEvent>? CaptureMade;

	/// <summary>
	///     Raised when an applied move castles.
	/// </summary>
	public event Action<GameMoveEvent>? CastlingMade;

	/// <summary>
	///     Raised when an applied move captures en passant.
	/// </summary>
	public event Action<GameMoveEvent>? EnPassantMade;

	/// <summary>
	///     Raised when a move requires a promotion choice before it can be applied.
	/// </summary>
	public event Action<PromotionRequiredEvent>? PromotionRequired;

	/// <summary>
	///     Raised when a pending promotion choice is resolved.
	/// </summary>
	public event Action<PromotionChosenEvent>? PromotionChosen;

	/// <summary>
	///     Raised when an applied move gives check.
	/// </summary>
	public event Action<GameMoveEvent>? Check;

	/// <summary>
	///     Raised when an applied move checkmates the opposing side.
	/// </summary>
	public event Action<GameMoveEvent>? Checkmate;

	/// <summary>
	///     Raised when an applied move stalemates the opposing side.
	/// </summary>
	public event Action<GameMoveEvent>? Stalemated;

	/// <summary>
	///     Raised when the side to move changes.
	/// </summary>
	public event Action<TurnChangedEvent>? TurnChanged;

	/// <summary>
	///     Raised when a move application is rejected.
	/// </summary>
	public event Action<IllegalMoveRejectedEvent>? IllegalMoveRejected;

	/// <summary>
	///     Raised when a draw offer is published.
	/// </summary>
	public event Action<UciState>? DrawOffered;

	/// <summary>
	///     Raised when a draw offer is declined or cleared explicitly.
	/// </summary>
	public event Action<UciState>? DrawDeclined;

	/// <summary>
	///     Raised when the match clock is paused.
	/// </summary>
	public event Action<UciState>? ClockPaused;

	/// <summary>
	///     Raised when the match clock is resumed.
	/// </summary>
	public event Action<UciState>? ClockResumed;

	/// <summary>
	///     Raised when one or more moves are undone.
	/// </summary>
	public event Action<MoveUndoneEvent>? MoveUndone;

	/// <summary>
	///     Raised when the engine starts thinking for the current position.
	/// </summary>
	public event Action<UciState>? EngineThinkingStarted;

	/// <summary>
	///     Raised when the engine stops thinking for the current position.
	/// </summary>
	public event Action<UciState>? EngineThinkingStopped;

	/// <summary>
	///     Obsolete alias for <see cref="Error"/>.
	/// </summary>
	[Obsolete("Use Error or EventPublished instead.")]
	public event Action<Exception>? EngineError;

	/// <summary>
	///     Raised when the coordinator has fully stopped.
	/// </summary>
	public event Action? Stopped;

	/// <summary>
	///     Raised for every canonical session event through a compact typed stream.
	/// </summary>
	public event Action<UciGameEngineSessionEvent>? EventPublished;

	/// <summary>
	///     Constructs a new UciGameEngineSession with engines initialized for the given enginePath, arguments, and working
	///     directory.
	/// </summary>
	/// <param name="enginePath">Path to the UCI engine executable.</param>
	/// <param name="args">Optional arguments for the engine.</param>
	/// <param name="workingDirectory">Optional working directory.</param>
	/// <param name="syncContext">Optional synchronization context to marshal events to (e.g. UI thread).</param>
	/// <param name="options">Optional configuration options for the coordinator.</param>
	/// <param name="perspectiveColor">The board perspective color, either <c>w</c> or <c>b</c>.</param>
	/// <param name="whiteController">Controller kind for the white side.</param>
	/// <param name="blackController">Controller kind for the black side.</param>
	public UciGameEngineSession(
		string                  enginePath,
		IEnumerable<string>?    args             = null,
		string?                 workingDirectory = null,
		SynchronizationContext? syncContext      = null,
		UciCoordinatorOptions?  options          = null,
		char                    perspectiveColor = 'w',
		MatchSideControllerKind whiteController  = MatchSideControllerKind.Manual,
		MatchSideControllerKind blackController  = MatchSideControllerKind.Manual)
		: this(
			new UciEngineClient(enginePath, args, workingDirectory),
			new UciPonderRuntime(new UciEngineClient(enginePath, args, workingDirectory)),
			new UciEngineClient(enginePath, args, workingDirectory),
			syncContext,
			options,
			perspectiveColor,
			whiteController,
			blackController
		) { }

	internal UciGameEngineSession(
		UciEngineClient         snapshotClient,
		UciPonderRuntime        ponder,
		UciEngineClient         classificationClient,
		SynchronizationContext? syncContext = null,
		UciCoordinatorOptions?  options     = null,
		char                    perspectiveColor = 'w',
		MatchSideControllerKind whiteController  = MatchSideControllerKind.Manual,
		MatchSideControllerKind blackController  = MatchSideControllerKind.Manual)
	{
		_events      = new(syncContext);
		_options     = options ?? UciCoordinatorOptions.Default;
		_snapshotClient = new(snapshotClient ?? throw new ArgumentNullException(nameof(snapshotClient)));
		_ponder      = ponder ?? throw new ArgumentNullException(nameof(ponder));
		_classificationClient = new(classificationClient ?? throw new ArgumentNullException(nameof(classificationClient)));
		_classificationRuntime = new(_sync);
		_perspectiveColor      = NormalizeColor(perspectiveColor, nameof(perspectiveColor));
		_whiteController       = whiteController;
		_blackController       = blackController;
		_timeControl           = _options.TimeControl;
		_timeControl?.Validate();
		_claimableDrawPolicy   = _options.ClaimableDrawPolicy;
		_drawOfferPolicy       = _options.DrawOfferPolicy;
		_controlledMoveFallbackPolicy = _options.ControlledMoveFallbackPolicy;

		_ponder.InfoPvWithGeneration   += OnPonderInfo;
		_ponder.BestMoveWithGeneration += PonderOnBestMove;
	}

	/// <summary>
	///     Gets a value indicating whether all underlying engines are healthy and responsive.
	/// </summary>
	public bool IsHealthy => _snapshotClient.IsHealthy && _ponder.IsHealthy && _classificationClient.IsHealthy;

	/// <summary>
	///     Gets a value indicating whether all underlying engines have been started.
	/// </summary>
	public bool IsStarted => _snapshotClient.IsStarted && _ponder.IsStarted && _classificationClient.IsStarted;

	/// <summary>
	///     Gets the current position FEN (after all played moves).
	/// </summary>
	public Fen CurrentFen => State.CurrentFen;

	/// <summary>
	///     Gets the list of legal moves in the current position.
	/// </summary>
	public IReadOnlyList<string> LegalMoves => State.LegalMoves;

	/// <summary>
	///     Gets the list of moves played from the starting FEN.
	/// </summary>
	public IReadOnlyList<string> PlayedMoves => State.PlayedMoves;

	/// <summary>
	///     Gets the current evaluation from the ponder engine.
	/// </summary>
	public PrincipalVariation? Evaluation => State.Evaluation;

	/// <summary>
	///     Gets the configuration options for this coordinator.
	/// </summary>
	public UciCoordinatorOptions Options => _options;

	/// <summary>
	///     Gets the engine metadata reported by the snapshot client instance.
	/// </summary>
	public UciEngineInfo EngineInfo => _snapshotClient.EngineInfo;

	/// <summary>
	///     Gets the options advertised by the snapshot client instance during handshake.
	/// </summary>
	public ImmutableArray<UciEngineOption> AvailableOptions => _snapshotClient.AvailableOptions;

	/// <summary>
	///     Gets the capability state detected for the configured engine.
	/// </summary>
	public UciEngineCapabilities Capabilities => _snapshotClient.Capabilities;

	/// <summary>
	///     Gets the side used for board-orientation and player-relative evaluation helpers.
	/// </summary>
	public char PerspectiveColor => _perspectiveColor;

	/// <summary>
	///     Gets the configured controller for White.
	/// </summary>
	public MatchSideControllerKind WhiteController => _whiteController;

	/// <summary>
	///     Gets the configured controller for Black.
	/// </summary>
	public MatchSideControllerKind BlackController => _blackController;

	/// <summary>
	///     Gets the current state of the coordinator.
	/// </summary>
	public UciState State
	{
		get
		{
			lock (_sync)
			{
				return _state;
			}
		}
		private set
		{
			lock (_sync)
			{
				_state = value;
			}

			RaiseStateEvent(StateChanged, UciGameEngineSessionEventKind.StateChanged, value);
		}
	}

	/// <summary>
	///     Creates and starts a new UciGameEngineSession with engines initialized and ready.
	/// </summary>
	/// <param name="enginePath">Path to the UCI engine executable.</param>
	/// <param name="options">Configuration options for the coordinator.</param>
	/// <param name="syncContext">Optional synchronization context to marshal events to (e.g. UI thread).</param>
	/// <param name="perspectiveColor">The board perspective color, either <c>w</c> or <c>b</c>.</param>
	/// <param name="whiteController">Controller kind for the white side.</param>
	/// <param name="blackController">Controller kind for the black side.</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>A fully initialized and started coordinator.</returns>
	public static async Task<UciGameEngineSession> CreateAsync(
		string                  enginePath,
		UciCoordinatorOptions?  options     = null,
		SynchronizationContext? syncContext = null,
		char                    perspectiveColor = 'w',
		MatchSideControllerKind whiteController  = MatchSideControllerKind.Manual,
		MatchSideControllerKind blackController  = MatchSideControllerKind.Manual,
		CancellationToken       ct          = default)
	{
		var coordinator = new UciGameEngineSession(
			enginePath,
			null,
			null,
			syncContext,
			options,
			perspectiveColor,
			whiteController,
			blackController
		);
		await coordinator.StartAsync(ct).ConfigureAwait(false);
		return coordinator;
	}

	/// <summary>
	///     Streams classified moves for the current position.
	///     Yields already classified moves first, then waits for new ones.
	///     Completes when all moves for the current position are classified.
	/// </summary>
	public async IAsyncEnumerable<Move> StreamClassifiedMovesAsync(
		[EnumeratorCancellation] CancellationToken ct = default)
	{
		var channel = Channel.CreateUnbounded<Move>();
		var yielded = new HashSet<string>();
		var yieldedGate = new object();

		UciState                                     initialState;
		Task<IReadOnlyDictionary<string, Move>>?     completionTask;
		lock (_sync)
		{
			initialState   = _state;
			completionTask = _classificationRuntime.CompletionTask;
		}

		foreach (var kvp in initialState.ClassifiedMoves)
		{
			yielded.Add(kvp.Key);
			yield return kvp.Value;
		}

		if (initialState.IsClassificationComplete)
			yield break;

		void OnStateChanged(UciState newState)
		{
			if (newState.BaseFen != initialState.BaseFen ||
				!newState.PlayedMoves.SequenceEqual(initialState.PlayedMoves))
			{
				channel.Writer.TryComplete();
				return;
			}

			lock (yieldedGate)
			{
				foreach (var kvp in newState.ClassifiedMoves)
				{
					if (yielded.Add(kvp.Key))
						channel.Writer.TryWrite(kvp.Value);
				}
			}

			if (newState.IsClassificationComplete)
				channel.Writer.TryComplete();
		}

		StateChanged += OnStateChanged;

		Task? completionMonitor = null;
		if (completionTask is { })
		{
			completionMonitor = completionTask.ContinueWith(
				static (task, state) =>
				{
					var writer = (ChannelWriter<Move>)state!;
					var error = task.IsFaulted ? task.Exception?.InnerException ?? task.Exception : null;
					writer.TryComplete(task.IsCanceled ? null : error);
				},
				channel.Writer,
				CancellationToken.None,
				TaskContinuationOptions.ExecuteSynchronously,
				TaskScheduler.Default
			);
		}

		try
		{
			while (await channel.Reader.WaitToReadAsync(ct).ConfigureAwait(false))
			{
				while (channel.Reader.TryRead(out var move))
				{
					yield return move;
				}
			}
		}
		finally
		{
			StateChanged -= OnStateChanged;
			if (completionMonitor is { })
				await completionMonitor.ConfigureAwait(false);
		}
	}

	/// <summary>
	///     Streams state changes as an async enumerable.
	///     Yields the current state immediately, then yields each subsequent state change.
	/// </summary>
	/// <param name="ct">Cancellation token.</param>
	public async IAsyncEnumerable<UciState> StreamStateAsync(
		[EnumeratorCancellation] CancellationToken ct = default)
	{
		var channel = Channel.CreateUnbounded<UciState>();

		void OnStateChanged(UciState state)
		{
			channel.Writer.TryWrite(state);
		}

		StateChanged += OnStateChanged;

		try
		{
			// Yield current state first
			yield return State;

			while (await channel.Reader.WaitToReadAsync(ct).ConfigureAwait(false))
			{
				while (channel.Reader.TryRead(out var state))
					yield return state;
			}
		}
		finally
		{
			StateChanged -= OnStateChanged;
			channel.Writer.TryComplete();
		}
	}

	/// <summary>
	///     Prepares a new game in all engines. Clears all cached move classifications and legal moves.
	/// </summary>
	public async Task NewGameAsync(CancellationToken ct = default)
	{
		InvalidatePonderState(clearTransientState: true);
		CoordinatorRuntimeUtilities.CancelAndDispose(ref _bestCts);
		ClearState();
		await Task.WhenAll(
			_snapshotClient.NewGameAsync(ct),
			_ponder.NewGameAsync(ct),
			_classificationClient.NewGameAsync(ct)
		).ConfigureAwait(false);

		Interlocked.Exchange(ref _acceptedPonderGeneration, 0);
		ResetGameplaySession();
		State = UciState.Default;
		var gameStarted = new GameStartedEvent(_gameId, Fen.Default, DateTimeOffset.UtcNow);
		RaiseTypedEvent(GameStarted, gameStarted, UciGameEngineSessionEvent.ForGameStarted(gameStarted));
	}

	/// <summary>
	///     Resets to the starting chess position.
	/// </summary>
	/// <param name="ct">Cancellation token.</param>
	public Task ResetAsync(CancellationToken ct = default) =>
		UpdatePositionAsync(Fen.Default, null, ct);

	/// <summary>
	///     Sets a UCI option on the ponder engine.
	/// </summary>
	/// <param name="name">The option name.</param>
	/// <param name="value">The option value, or null to use the engine's default.</param>
	/// <param name="ct">Cancellation token.</param>
	public Task SetOptionAsync(string name, string? value, CancellationToken ct = default) =>
		Task.WhenAll(
			_snapshotClient.SetOptionAsync(name, value, ct),
			_ponder.SetOptionAsync(name, value, ct),
			_classificationClient.SetOptionAsync(name, value, ct)
		);

	/// <summary>
	///     Sends the standard UCI <c>debug on/off</c> command to all internal engines.
	/// </summary>
	public Task SetDebugAsync(bool enabled, CancellationToken ct = default) =>
		Task.WhenAll(
			_snapshotClient.SetDebugAsync(enabled, ct),
			_ponder.SetDebugAsync(enabled, ct),
			_classificationClient.SetDebugAsync(enabled, ct)
		);

	/// <summary>
	///     Sends the standard UCI <c>register</c> command to all internal engines.
	/// </summary>
	public Task RegisterAsync(UciRegistration registration, CancellationToken ct = default) =>
		Task.WhenAll(
			_snapshotClient.RegisterAsync(registration, ct),
			_ponder.RegisterAsync(registration, ct),
			_classificationClient.RegisterAsync(registration, ct)
		);

	/// <summary>
	///     Sets the position from a FEN string.
	/// </summary>
	/// <param name="fen">The FEN string representing the position.</param>
	/// <param name="ct">Cancellation token.</param>
	/// <exception cref="ArgumentException">Thrown when the FEN string is invalid.</exception>
	public Task SetPositionAsync(string fen, CancellationToken ct = default)
	{
		var parsed = Fen.Parse(fen);
		if (!parsed.HasValue)
			throw new ArgumentException($"Invalid FEN string: '{fen}'", nameof(fen));

		return UpdatePositionAsync(parsed.Value, null, ct);
	}

	/// <summary>
	///     Starts all engines and resets state. Sets UCI options on the ponder engine based on configuration.
	/// </summary>
	public async Task StartAsync(CancellationToken ct = default)
	{
		var snapshotStarted   = false;
		var ponderStarted     = false;
		var classifierStarted = false;

		try
		{
			await _snapshotClient.StartWithCoordinatorCapabilitiesAsync(ct).ConfigureAwait(false);
			CoordinatorRuntimeUtilities.EnsureCoordinatorCapabilities(_snapshotClient.Capabilities);
			snapshotStarted = true;

			await _ponder.StartAsync(ct).ConfigureAwait(false);
			ponderStarted = true;

			await _classificationClient.StartAsync(ct).ConfigureAwait(false);
			classifierStarted = true;

			// Configure ponder engine based on options.
			await _ponder.SetOptionAsync("Threads", _options.PonderThreads.ToString(), ct).ConfigureAwait(false);
			await _ponder.SetOptionAsync("MultiPv", _options.MultiPv.ToString(),       ct).ConfigureAwait(false);

			ClearState();
			ResetGameplaySession();
			State = UciState.Default;

			var gameStarted = new GameStartedEvent(_gameId, Fen.Default, DateTimeOffset.UtcNow);
			RaiseTypedEvent(GameStarted, gameStarted, UciGameEngineSessionEvent.ForGameStarted(gameStarted));
			RaiseSessionEvent(Ready, UciGameEngineSessionEventKind.Ready);
		}
		catch
		{
			await RollbackStartAsync(snapshotStarted, ponderStarted, classifierStarted).ConfigureAwait(false);
			throw;
		}
	}

	/// <summary>
	///     Begins an infinite search from the current position. Cancels any previous search.
	/// </summary>
	/// <param name="ct">Cancellation token.</param>
	public Task StartSearchAsync(CancellationToken ct = default) =>
		StartSearchAsync(CurrentFen, null, ct);

	/// <summary>
	///     Begins an infinite search from the specified position. Cancels any previous search.
	/// </summary>
	/// <param name="fen">Position to search from.</param>
	/// <param name="playedMoves">Optional played moves to append to the FEN.</param>
	/// <param name="ct">Cancellation token.</param>
	public async Task StartSearchAsync(
		Fen                  fen,
		IEnumerable<string>? playedMoves = null,
		CancellationToken    ct          = default)
	{
		InvalidatePonderState(clearTransientState: true);

		var newCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
		var token  = newCts.Token;
		CoordinatorRuntimeUtilities.CancelAndDispose(ref _bestCts, newCts);

		try
		{
			await _ponder.StartSearchAsync(fen, playedMoves, token).ConfigureAwait(false);
			AcceptPonderGeneration(_ponder.CurrentSearchGeneration);
			SetSearchingState(true);
		}
		catch
		{
			if (CoordinatorRuntimeUtilities.CancelAndDisposeIfCurrent(ref _bestCts, newCts))
				SetSearchingState(false);

			throw;
		}
	}

	/// <summary>
	///     Stops any ongoing search and all engine operations. Resets cached state and cancels in-flight move classification.
	/// </summary>
	public async Task StopAsync(CancellationToken ct = default)
	{
		var stopFailures = new List<Exception>();
		bool cancellationRequested = ct.IsCancellationRequested;
		ClearState();

		cancellationRequested = await TryStopStepAsync(
			StopSearchCoreAsync,
			cancellationRequested,
			stopFailures,
			ct,
			reportErrors: false
		).ConfigureAwait(false);
		cancellationRequested = await TryStopStepAsync(
			_classificationClient.StopAsync,
			cancellationRequested,
			stopFailures,
			ct
		).ConfigureAwait(false);
		cancellationRequested = await TryStopStepAsync(
			token => _ponder.StopAsync(token),
			cancellationRequested,
			stopFailures,
			ct
		).ConfigureAwait(false);
		cancellationRequested = await TryStopStepAsync(
			_snapshotClient.StopAsync,
			cancellationRequested,
			stopFailures,
			ct
		).ConfigureAwait(false);

		Interlocked.Exchange(ref _acceptedPonderGeneration, 0);
		State = UciState.Default;

		RaiseSessionEvent(Stopped, UciGameEngineSessionEventKind.Stopped);
		CoordinatorRuntimeUtilities.ThrowStopFailures(stopFailures, cancellationRequested, ct);
	}

	/// <summary>
	///     Stops any ongoing infinite ponder search.
	/// </summary>
	public async Task StopSearchAsync(CancellationToken ct = default)
	{
		try
		{
			await StopSearchCoreAsync(ct).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			// Expected when cancellation is requested
		}
		catch (Exception ex)
		{
			RaiseError(ex);
		}
	}

	/// <summary>
	///     Fully updates the engines to reflect a new board position:
	///     - Stops prior search and cancels in-flight classification.
	///     - Updates all engines to the new position.
	///     - Publishes new set of legal moves via StateChanged.
	///     - Kicks off ponder search and background classification.
	/// </summary>
	/// <param name="fen">Position FEN</param>
	/// <param name="playedMoves">Optional moves played after the FEN</param>
	/// <param name="ct">Cancellation token</param>
	public async Task UpdatePositionAsync(
		Fen                  fen,
		IEnumerable<string>? playedMoves,
		CancellationToken    ct = default)
	{
		var movesList = playedMoves?.ToList() ?? new List<string>();
		var previousTurn = State.CurrentFen.ActiveColor;

		ResetGameplaySession();
		var snapshot = await LoadPositionSnapshotAsync(fen, movesList, ct).ConfigureAwait(false);
		InitializeClocks(snapshot.CurrentFen.ActiveColor);
		var metadata = ApplyMatchMetadata(snapshot);
		snapshot = metadata.Snapshot;
		if (metadata.ResultChanged)
			RaiseStateEvent(ResultChanged, UciGameEngineSessionEventKind.ResultChanged, snapshot);

		var positionLoaded = new PositionLoadedEvent(
			_gameId,
			snapshot.BaseFen,
			snapshot.CurrentFen,
			[.. snapshot.PlayedMoves],
			DateTimeOffset.UtcNow
		);
		RaiseTypedEvent(PositionLoaded, positionLoaded, UciGameEngineSessionEvent.ForPositionLoaded(positionLoaded));
		PublishPositionChanged(snapshot);
		RaiseStateEvent(LegalMovesUpdated, UciGameEngineSessionEventKind.LegalMovesUpdated, snapshot);
		if (previousTurn != snapshot.CurrentFen.ActiveColor)
		{
			var turnChanged = new TurnChangedEvent(
				_gameId,
				previousTurn,
				snapshot.CurrentFen.ActiveColor,
				snapshot.CurrentFen,
				DateTimeOffset.UtcNow
			);
			RaiseTypedEvent(TurnChanged, turnChanged, UciGameEngineSessionEvent.ForTurnChanged(turnChanged));
		}
		PublishGameOver(snapshot);

		await StartBackgroundWorkAsync(fen, movesList, snapshot.CurrentFen, snapshot.LegalMoves, ct).ConfigureAwait(false);
	}

	/// <summary>
	///     Gets the actual current FEN (after all played moves) as seen by the engine.
	/// </summary>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>The current position FEN, or null if not available.</returns>
	public Task<Fen?> GetCurrentFenAsync(CancellationToken ct = default) =>
		_snapshotClient.GetFenAsync(ct);

	/// <summary>
	///     Waits until all legal moves in the current position have been classified.
	/// </summary>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>Dictionary of all classified moves.</returns>
	public async Task<IReadOnlyDictionary<string, Move>> WaitForClassificationAsync(CancellationToken ct = default)
	{
		Task<IReadOnlyDictionary<string, Move>> waitTask;
		lock (_sync)
		{
			if (_state.IsClassificationComplete)
				return _state.ClassifiedMoves;

			waitTask = _classificationRuntime.CompletionTask ??
					   Task.FromResult<IReadOnlyDictionary<string, Move>>(_state.ClassifiedMoves);
		}

		if (!ct.CanBeCanceled)
			return await waitTask.ConfigureAwait(false);

		var cancelTcs = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
		using var registration = ct.Register(static state => ((TaskCompletionSource<object?>)state!).TrySetResult(null), cancelTcs);

		var completed = await Task.WhenAny(waitTask, cancelTcs.Task).ConfigureAwait(false);
		if (completed == cancelTcs.Task)
			throw new OperationCanceledException(ct);

		return await waitTask.ConfigureAwait(false);
	}

	/// <summary>
	///     Classifies a single move in the current position.
	/// </summary>
	/// <param name="move">The move to classify (in UCI notation).</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>The classified move with analysis.</returns>
	/// <exception cref="ArgumentException">Thrown when the move is not legal.</exception>
	public async Task<Move> ClassifyMoveAsync(string move, CancellationToken ct = default)
	{
		var currentState = State;

		return await ClassifyMoveForStateAsync(currentState, move, ct).ConfigureAwait(false);
	}

	internal async Task<Move> ClassifyMoveForStateAsync(UciState currentState, string move, CancellationToken ct = default)
	{
		if (!currentState.LegalMoves.Contains(move))
			throw new ArgumentException($"Move '{move}' is not legal in the current position.", nameof(move));

		// Check if already classified
		if (currentState.ClassifiedMoves.TryGetValue(move, out var existing))
			return existing;

		return await ClassifyMoveWithClassificationClientAsync(currentState.CurrentFen, move, ct).ConfigureAwait(false);
	}

	private async Task<Move> ClassifyMoveForEventAsync(
		UciState           currentState,
		string             move,
		CancellationToken  ct = default)
	{
		try
		{
			return await ClassifyMoveForStateAsync(currentState, move, ct).ConfigureAwait(false);
		}
		catch (Exception) when (CoordinatorMoveFactory.TryBuildFallbackMove(currentState.CurrentFen, move, out var fallbackMove))
		{
			return fallbackMove;
		}
	}

	/// <summary>
	///     Performs a blocking search with the specified parameters and returns the result.
	/// </summary>
	/// <param name="parameters">Search parameters (depth, time, nodes, etc.).</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>The search result containing the best move and evaluation.</returns>
	public Task<SearchResult> SearchAsync(SearchParameters parameters, CancellationToken ct = default) =>
		_snapshotClient.SearchAsync(CurrentFen, null, parameters, ct);

	/// <summary>
	///     Applies a move to the current position and updates the engines.
	///     This is a convenience wrapper around <see cref="UpdatePositionAsync" />.
	/// </summary>
	/// <param name="move">The move to play (in UCI notation, e.g. "e2e4").</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>The new state after the move is applied.</returns>
	/// <exception cref="ArgumentException">Thrown when the move is not legal in the current position.</exception>
	public async Task<UciState> MakeMoveAsync(string move, CancellationToken ct = default)
	{
		return await MakeMoveAsync(move, GameMoveActor.Human, ct).ConfigureAwait(false);
	}

	/// <summary>
	///     Plays the current side automatically when that side is configured as engine-controlled.
	/// </summary>
	public async Task<EngineMoveResult> PlayControlledMoveAsync(CancellationToken ct = default)
	{
		if (GetController(CurrentFen.ActiveColor) != MatchSideControllerKind.Engine)
			throw new InvalidOperationException("The current side is not configured for engine control.");

		try
		{
			var result = await SearchAsync(new() { MoveTimeMs = _options.EngineMoveTimeMs }, ct).ConfigureAwait(false);
			await MakeMoveAsync(result.BestMove, GameMoveActor.Engine, ct).ConfigureAwait(false);
			return new(result.BestMove.ToLowerInvariant(), result);
		}
		catch (InvalidOperationException) when (
			_controlledMoveFallbackPolicy == PlayableMatchControlledMoveFallbackPolicy.UseLocalFallback &&
			TryResolveFallbackControlledMove(State, out var fallbackMove))
		{
			await MakeMoveAsync(fallbackMove, GameMoveActor.Engine, ct).ConfigureAwait(false);
			return new(fallbackMove, default);
		}
	}

	/// <summary>
	///     Applies a move to the current position and updates the engines with an explicit actor classification.
	/// </summary>
	/// <param name="move">The move to play (in UCI notation, e.g. "e2e4").</param>
	/// <param name="actor">Who initiated the move.</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>The new state after the move is applied.</returns>
	/// <exception cref="ArgumentException">Thrown when the move is not legal in the current position.</exception>
	public async Task<UciState> MakeMoveAsync(string move, GameMoveActor actor, CancellationToken ct = default)
	{
		UciState currentState;
		CoordinatorPendingPromotionRequest? pendingPromotion;
		lock (_sync)
		{
			currentState     = _state;
			pendingPromotion = _pendingPromotion;
		}

		string normalizedMove = move?.Trim().ToLowerInvariant() ?? string.Empty;
		EnsureTurnHasTimeRemaining();

		if (pendingPromotion.HasValue)
		{
			RejectIllegalMove(
				normalizedMove,
				"A promotion choice is pending. Resolve it before applying another move.",
				currentState.LegalMoves,
				true
			);
		}

		long pendingPromotionId = Interlocked.Increment(ref _nextPendingPromotionId);

		if (CoordinatorMoveFactory.TryCreatePromotionRequest(
				_gameId,
				pendingPromotionId,
				currentState,
				normalizedMove,
				actor,
				out var request))
		{
			lock (_sync)
			{
				_pendingPromotion = new(
					request.PendingPromotionId,
					request.Actor,
					request.From,
					request.To,
					request.MovingPiece,
					request.AllowedPromotionPieces,
					currentState
				);
			}

			RaiseTypedEvent(
				PromotionRequired,
				request,
				UciGameEngineSessionEvent.ForPromotionRequired(request)
			);
			return currentState;
		}

		if (!currentState.LegalMoves.Contains(normalizedMove))
			RejectIllegalMove(
				normalizedMove,
				$"Move '{normalizedMove}' is not legal in the current position.",
				currentState.LegalMoves,
				false
			);

		var classifiedMove = await ClassifyMoveForEventAsync(currentState, normalizedMove, ct).ConfigureAwait(false);
		return await ApplyMoveAsync(currentState, normalizedMove, classifiedMove, actor, null, ct).ConfigureAwait(false);
	}

	/// <summary>
	///     Resolves a pending promotion choice and applies the completed move.
	/// </summary>
	/// <param name="pendingPromotionId">The identifier previously published in <see cref="PromotionRequired" />.</param>
	/// <param name="pieceType">The chosen promotion piece type.</param>
	/// <param name="ct">Cancellation token.</param>
	public async Task<UciState> ChoosePromotionAsync(
		long             pendingPromotionId,
		PieceType        pieceType,
		CancellationToken ct = default)
	{
		CoordinatorPendingPromotionRequest pending;
		lock (_sync)
		{
			if (!_pendingPromotion.HasValue || _pendingPromotion.Value.Id != pendingPromotionId)
				throw new InvalidOperationException("No matching pending promotion request exists.");

			pending           = _pendingPromotion.Value;
			_pendingPromotion = null;
		}

		string? notation = CoordinatorMoveFactory.ResolvePromotionMoveNotation(pending, pieceType);
		if (notation is null)
			throw new ArgumentException($"Promotion piece '{pieceType}' is not allowed for the pending move.", nameof(pieceType));

		var promotionChosen = new PromotionChosenEvent(
			_gameId,
			pending.Id,
			notation,
			pieceType,
			DateTimeOffset.UtcNow
		);

		var classifiedMove = await ClassifyMoveForEventAsync(pending.State, notation, ct).ConfigureAwait(false);
		return await ApplyMoveAsync(
			pending.State,
			notation,
			classifiedMove,
			pending.Actor,
			promotionChosen,
			ct
		).ConfigureAwait(false);
	}

	/// <summary>
	///     Reverts one or more moves and updates the engines.
	/// </summary>
	/// <param name="count">Number of moves to undo. Defaults to 1.</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>The new state after undoing moves, or the current state if no moves were undone.</returns>
	public async Task<UciState> UndoAsync(int count = 1, CancellationToken ct = default)
	{
		if (count < 1)
			throw new ArgumentOutOfRangeException(nameof(count), "Count must be at least 1.");

		UciState currentState;
		ImmutableArray<GameMoveEvent> appliedMoves;
		lock (_sync)
		{
			currentState = _state;
			appliedMoves = _appliedMoveHistory;
			_pendingPromotion = null;
		}

		if (currentState.PlayedMoves.Count == 0)
			return currentState;

		int movesToRemove = Math.Min(count, currentState.PlayedMoves.Count);
		var newMoves      = currentState.PlayedMoves.Take(currentState.PlayedMoves.Count - movesToRemove).ToList();
		var previousTurn = currentState.CurrentFen.ActiveColor;
		var snapshot = await LoadPositionSnapshotAsync(currentState.BaseFen, newMoves, ct).ConfigureAwait(false);
		InitializeClocks(snapshot.CurrentFen.ActiveColor);
		var metadata = ApplyMatchMetadata(snapshot);
		snapshot = metadata.Snapshot;

		var removedMoves = appliedMoves.Length >= movesToRemove
							   ? appliedMoves[^movesToRemove..]
							   : appliedMoves;

		lock (_sync)
		{
			_appliedMoveHistory = appliedMoves[..Math.Max(0, appliedMoves.Length - removedMoves.Length)];
		}

		var moveUndone = new MoveUndoneEvent(_gameId, removedMoves, snapshot.CurrentFen, DateTimeOffset.UtcNow);
		RaiseTypedEvent(MoveUndone, moveUndone, UciGameEngineSessionEvent.ForMoveUndone(moveUndone));
		if (metadata.ResultChanged)
			RaiseStateEvent(ResultChanged, UciGameEngineSessionEventKind.ResultChanged, snapshot);
		if (previousTurn != snapshot.CurrentFen.ActiveColor)
		{
			var turnChanged = new TurnChangedEvent(
				_gameId,
				previousTurn,
				snapshot.CurrentFen.ActiveColor,
				snapshot.CurrentFen,
				DateTimeOffset.UtcNow
			);
			RaiseTypedEvent(TurnChanged, turnChanged, UciGameEngineSessionEvent.ForTurnChanged(turnChanged));
		}
		PublishPositionChanged(snapshot);
		RaiseStateEvent(LegalMovesUpdated, UciGameEngineSessionEventKind.LegalMovesUpdated, snapshot);
		PublishGameOver(snapshot);

		await StartBackgroundWorkAsync(
			currentState.BaseFen,
			newMoves,
			snapshot.CurrentFen,
			snapshot.LegalMoves,
			ct
		).ConfigureAwait(false);
		return State;
	}

	/// <summary>
	///     Offers a draw from the current side.
	/// </summary>
	public Task<UciState> OfferDrawAsync(CancellationToken ct = default)
	{
		ct.ThrowIfCancellationRequested();
		var snapshot = UpdateMatchMetadata(drawOfferedBy: State.CurrentFen.ActiveColor);
		RaiseStateEvent(DrawOffered, UciGameEngineSessionEventKind.DrawOffered, snapshot);
		return Task.FromResult(snapshot);
	}

	/// <summary>
	///     Accepts the current pending draw offer and ends the game as a draw.
	/// </summary>
	public Task<UciState> AcceptDrawAsync(CancellationToken ct = default)
	{
		ct.ThrowIfCancellationRequested();
		var snapshot = SetForcedResult(new(PlayableMatchResultReason.DrawAgreement, null));
		return Task.FromResult(snapshot);
	}

	/// <summary>
	///     Declines the current pending draw offer.
	/// </summary>
	public Task<UciState> DeclineDrawAsync(CancellationToken ct = default)
	{
		ct.ThrowIfCancellationRequested();
		var snapshot = UpdateMatchMetadata(drawOfferedBy: null);
		RaiseStateEvent(DrawDeclined, UciGameEngineSessionEventKind.DrawDeclined, snapshot);
		return Task.FromResult(snapshot);
	}

	/// <summary>
	///     Claims the currently claimable draw result when available.
	/// </summary>
	public Task<UciState> ClaimDrawAsync(CancellationToken ct = default)
	{
		ct.ThrowIfCancellationRequested();
		if (!State.ClaimableResult.HasValue)
			throw new InvalidOperationException("There is no claimable draw available.");

		var snapshot = SetForcedResult(State.ClaimableResult.Value);
		return Task.FromResult(snapshot);
	}

	/// <summary>
	///     Resigns on behalf of the current side.
	/// </summary>
	public Task<UciState> ResignAsync(CancellationToken ct = default)
	{
		ct.ThrowIfCancellationRequested();
		var snapshot = SetForcedResult(new(PlayableMatchResultReason.Resignation, Opposite(CurrentFen.ActiveColor)));
		return Task.FromResult(snapshot);
	}

	/// <summary>
	///     Pauses the active match clock when time control is enabled.
	/// </summary>
	public Task<UciState> PauseClockAsync(CancellationToken ct = default)
	{
		ct.ThrowIfCancellationRequested();
		if (!_timeControl.HasValue || _isClockPaused || _clockHistory.Count == 0)
			return Task.FromResult(State);

		var checkpoint = _clockHistory[^1];
		_clockHistory[^1] = checkpoint with { PausedAtUtc = DateTimeOffset.UtcNow };
		_isClockPaused = true;
		var snapshot = UpdateMatchMetadata();
		RaiseStateEvent(ClockPaused, UciGameEngineSessionEventKind.ClockPaused, snapshot);
		return Task.FromResult(snapshot);
	}

	/// <summary>
	///     Resumes the active match clock when paused.
	/// </summary>
	public Task<UciState> ResumeClockAsync(CancellationToken ct = default)
	{
		ct.ThrowIfCancellationRequested();
		if (!_timeControl.HasValue || !_isClockPaused || _clockHistory.Count == 0)
			return Task.FromResult(State);

		var checkpoint = _clockHistory[^1];
		var now = DateTimeOffset.UtcNow;
		var pausedDuration = checkpoint.PausedAtUtc.HasValue ? now - checkpoint.PausedAtUtc.Value : TimeSpan.Zero;
		_clockHistory[^1] = checkpoint with
		{
			PausedAccumulated = checkpoint.PausedAccumulated + pausedDuration,
			PausedAtUtc = null
		};
		_isClockPaused = false;
		var snapshot = UpdateMatchMetadata();
		RaiseStateEvent(ClockResumed, UciGameEngineSessionEventKind.ClockResumed, snapshot);
		return Task.FromResult(snapshot);
	}

	/// <summary>
	///     Returns the configured controller for the supplied side.
	/// </summary>
	public MatchSideControllerKind GetController(char side) => NormalizeColor(side, nameof(side)) switch
	{
		'w' => _whiteController,
		_ => _blackController
	};

	/// <summary>
	///     Disposes all underlying engines and cancels outstanding classification.
	/// </summary>
	public async ValueTask DisposeAsync()
	{
		// Cancel any background classification
		ClearState();

		// Unsubscribe from engine events
		_ponder.InfoPvWithGeneration   -= OnPonderInfo;
		_ponder.BestMoveWithGeneration -= PonderOnBestMove;

		// Dispose cancellation token sources
		// (ClearState() already handled _classificationCts)
		CoordinatorRuntimeUtilities.CancelAndDispose(ref _bestCts);

		await _classificationClient.DisposeAsync();
		await _ponder.DisposeAsync();
		await _snapshotClient.DisposeAsync();
	}

	/// <summary>
	///     Synchronously disposes all underlying engines and cancels outstanding classification.
	/// </summary>
	public void Dispose()
	{
		DisposeAsync().AsTask().GetAwaiter().GetResult();
		GC.SuppressFinalize(this);
	}

	/// <summary>
	///     Cancels background move classification and clears all cached per-move results.
	/// </summary>
	private void ClearState()
	{
		CoordinatorRuntimeUtilities.CancelAndDispose(ref _classificationCts);
		CancelClassificationCompletion();
	}

	private void ResetGameplaySession()
	{
		lock (_sync)
		{
			_gameId                 = Guid.NewGuid();
			_nextMoveId             = 0;
			_nextPendingPromotionId = 0;
			_appliedMoveHistory     = [];
			_pendingPromotion       = null;
			_forcedResult           = default;
			_claimableResult        = null;
			_drawOfferedBy          = null;
			_isClockPaused          = false;
			_clockHistory.Clear();
			_lastResult = default;
		}
	}

	private static char NormalizeColor(char color, string paramName)
	{
		if (color is 'w' or 'b')
			return color;

		throw new ArgumentOutOfRangeException(paramName, "Color must be 'w' or 'b'.");
	}

	private async Task<Move> ClassifyMoveWithClassificationClientAsync(
		Fen               fen,
		string            move,
		CancellationToken ct)
	{
		var result = await _classificationClient.SearchMoveAsync(fen, move, _options.ClassificationDepth, ct)
												.ConfigureAwait(false);
		var boardState = BoardState.FromFen(fen) ??
						 throw new InvalidOperationException(
							 $"Unable to build board state from FEN '{fen.Raw}' for move classification."
						 );
		var score = MoveScore.FromSearchResult(result);
		return new Move(move, MoveAnalysis.Analyze(move, boardState, score, false));
	}

	private async Task<UciState> LoadPositionSnapshotAsync(
		Fen                 fen,
		IReadOnlyCollection<string> moves,
		CancellationToken   ct)
	{
		await StopSearchAsync(ct).ConfigureAwait(false);
		ClearState();

		await _snapshotClient.SetPositionAsync(fen, moves, ct).ConfigureAwait(false);
		await _ponder.SetPositionAsync(fen, moves, ct).ConfigureAwait(false);

		var legalMoves = await _snapshotClient.GetLegalMovesAsync(ct).ConfigureAwait(false);
		var effectiveFen = await _snapshotClient.GetFenAsync(ct).ConfigureAwait(false) ?? fen;

		lock (_sync)
		{
			_state = new(
				fen,
				effectiveFen,
				moves.ToImmutableList(),
				legalMoves.ToImmutableList(),
				ImmutableDictionary<string, Move>.Empty,
				null,
				null,
				null,
				false
			);

			return _state;
		}
	}

	private async Task StartBackgroundWorkAsync(
		Fen                  baseFen,
		IReadOnlyCollection<string> moves,
		Fen                  currentFen,
		ImmutableList<string> legalMoves,
		CancellationToken    ct)
	{
		try
		{
			await StartSearchAsync(baseFen, moves, ct).ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (ct.IsCancellationRequested)
		{
			throw;
		}
		catch (Exception ex)
		{
			RaiseError(ex);
		}

		int classificationGeneration = BeginClassificationRun();
		if (legalMoves.Count == 0)
		{
			CompleteClassificationRun(classificationGeneration);
			return;
		}

		var classificationCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
		var classificationToken = classificationCts.Token;
		CoordinatorRuntimeUtilities.CancelAndDispose(ref _classificationCts, classificationCts);

		_ = Task.Run(
			async () =>
			{
				try
				{
					foreach (var legalMove in legalMoves)
					{
						classificationToken.ThrowIfCancellationRequested();
						var move = await ClassifyMoveWithClassificationClientAsync(
							currentFen,
							legalMove,
							classificationToken
						).ConfigureAwait(false);
						ApplyClassifiedMove(move, classificationGeneration);
					}

					CompleteClassificationRun(classificationGeneration);
				}
				catch (OperationCanceledException)
				{
					CancelClassificationRunIfActive(classificationGeneration);
				}
				catch (Exception ex)
				{
					FaultClassificationRun(classificationGeneration, ex);
					RaiseError(ex);
				}
			},
			CancellationToken.None
		);
	}

	private async Task<UciState> ApplyMoveAsync(
		UciState                previousState,
		string                  moveNotation,
		Move                    classifiedMove,
		GameMoveActor           actor,
		PromotionChosenEvent?   promotionChosenEvent,
		CancellationToken       ct)
	{
		AdvanceClockForCompletedMove(previousState.CurrentFen.ActiveColor);
		var newMoves = new List<string>(previousState.PlayedMoves) { moveNotation };
		_forcedResult = default;
		if (_drawOfferPolicy == PlayableMatchDrawOfferPolicy.ExpireOnMove)
			_drawOfferedBy = null;

		var snapshot = await LoadPositionSnapshotAsync(previousState.BaseFen, newMoves, ct).ConfigureAwait(false);
		long moveId = Interlocked.Increment(ref _nextMoveId);

		var moveEvent = CoordinatorMoveFactory.BuildGameMoveEvent(
			_gameId,
			moveId,
			previousState,
			snapshot,
			moveNotation,
			classifiedMove,
			actor
		);

		lock (_sync)
		{
			_appliedMoveHistory = _appliedMoveHistory.Add(moveEvent);
			_pendingPromotion   = null;
		}

		var metadata = ApplyMatchMetadata(snapshot);
		snapshot = metadata.Snapshot;

		if (promotionChosenEvent.HasValue)
			RaiseTypedEvent(
				PromotionChosen,
				promotionChosenEvent.Value,
				UciGameEngineSessionEvent.ForPromotionChosen(promotionChosenEvent.Value)
			);

		RaiseTypedEvent(MoveMade, moveEvent, UciGameEngineSessionEvent.ForMove(UciGameEngineSessionEventKind.MoveMade, moveEvent));
		if (moveEvent.KindFlags.HasFlag(GameMoveKindFlags.Capture))
			RaiseTypedEvent(CaptureMade, moveEvent, UciGameEngineSessionEvent.ForMove(UciGameEngineSessionEventKind.CaptureMade, moveEvent));
		if (moveEvent.KindFlags.HasFlag(GameMoveKindFlags.KingsideCastling) ||
			moveEvent.KindFlags.HasFlag(GameMoveKindFlags.QueensideCastling))
			RaiseTypedEvent(CastlingMade, moveEvent, UciGameEngineSessionEvent.ForMove(UciGameEngineSessionEventKind.CastlingMade, moveEvent));
		if (moveEvent.KindFlags.HasFlag(GameMoveKindFlags.EnPassant))
			RaiseTypedEvent(EnPassantMade, moveEvent, UciGameEngineSessionEvent.ForMove(UciGameEngineSessionEventKind.EnPassantMade, moveEvent));
		if (moveEvent.IsCheck)
			RaiseTypedEvent(Check, moveEvent, UciGameEngineSessionEvent.ForMove(UciGameEngineSessionEventKind.Check, moveEvent));
		if (moveEvent.IsCheckmate)
			RaiseTypedEvent(Checkmate, moveEvent, UciGameEngineSessionEvent.ForMove(UciGameEngineSessionEventKind.Checkmate, moveEvent));
		if (moveEvent.IsStalemate)
			RaiseTypedEvent(Stalemated, moveEvent, UciGameEngineSessionEvent.ForMove(UciGameEngineSessionEventKind.Stalemated, moveEvent));
		if (metadata.ResultChanged)
			RaiseStateEvent(ResultChanged, UciGameEngineSessionEventKind.ResultChanged, snapshot);

		PublishGameOver(snapshot);
		if (previousState.CurrentFen.ActiveColor != snapshot.CurrentFen.ActiveColor)
		{
			var turnChanged = new TurnChangedEvent(
				_gameId,
				previousState.CurrentFen.ActiveColor,
				snapshot.CurrentFen.ActiveColor,
				snapshot.CurrentFen,
				DateTimeOffset.UtcNow
			);
			RaiseTypedEvent(TurnChanged, turnChanged, UciGameEngineSessionEvent.ForTurnChanged(turnChanged));
		}
		PublishPositionChanged(snapshot);
		RaiseStateEvent(LegalMovesUpdated, UciGameEngineSessionEventKind.LegalMovesUpdated, snapshot);

		await StartBackgroundWorkAsync(
			previousState.BaseFen,
			newMoves,
			snapshot.CurrentFen,
			snapshot.LegalMoves,
			ct
		).ConfigureAwait(false);
		return State;
	}

	private void RejectIllegalMove(
		string                 move,
		string                 reason,
		IReadOnlyList<string>  legalMoves,
		bool                   isPromotionChoicePending)
	{
		var illegalMove = new IllegalMoveRejectedEvent(
			_gameId,
			move,
			reason,
			[.. legalMoves],
			isPromotionChoicePending,
			DateTimeOffset.UtcNow
		);
		RaiseTypedEvent(IllegalMoveRejected, illegalMove, UciGameEngineSessionEvent.ForIllegalMove(illegalMove));
		throw new ArgumentException(reason, nameof(move));
	}

	private UciState UpdateMatchMetadata(char? drawOfferedBy = null)
	{
		if (drawOfferedBy.HasValue || _drawOfferedBy.HasValue)
			_drawOfferedBy = drawOfferedBy;

		var update = ApplyMatchMetadata(State);
		if (update.ResultChanged)
		{
			RaiseStateEvent(ResultChanged, UciGameEngineSessionEventKind.ResultChanged, update.Snapshot);
			PublishGameOver(update.Snapshot);
		}

		return update.Snapshot;
	}

	private UciState SetForcedResult(PlayableMatchResult result)
	{
		_forcedResult = result;
		_claimableResult = null;
		_drawOfferedBy = null;
		return UpdateMatchMetadata();
	}

	private MetadataUpdate ApplyMatchMetadata(UciState state)
	{
		var clock = GetClockSnapshot();
		var outcome = EvaluateOutcome(state.CurrentFen, state.LegalMoves, clock);
		var snapshot = state with
		{
			Result = outcome.Result,
			ClaimableResult = outcome.ClaimableResult,
			DrawOfferedBy = _drawOfferedBy,
			Clock = clock
		};

		bool resultChanged;
		lock (_sync)
		{
			resultChanged = _lastResult != outcome.Result;
			_claimableResult = outcome.ClaimableResult;
			_lastResult = outcome.Result;
			_state = snapshot;
		}

		return new(snapshot, resultChanged);
	}

	private static bool TryResolveFallbackControlledMove(UciState state, out string move)
	{
		move = string.Empty;
		if (state.LegalMoves.Count == 0)
			return false;

		string? checkMove = null;
		string? captureMove = null;
		string? promotionMove = null;

		foreach (var legalMove in state.LegalMoves)
		{
			var classification = state.CurrentFen.ClassifyMoveFully(legalMove);
			if (classification.IsMate)
			{
				move = legalMove;
				return true;
			}

			if (checkMove is null && classification.IsCheck)
				checkMove = legalMove;

			if (captureMove is null && classification.IsCapture)
				captureMove = legalMove;

			if (promotionMove is null && classification.IsPromotion)
				promotionMove = legalMove;
		}

		move = checkMove ?? captureMove ?? promotionMove ?? state.LegalMoves[0];
		return true;
	}

	private void InitializeClocks(char activeColor)
	{
		_clockHistory.Clear();
		if (!_timeControl.HasValue)
			return;

		_clockHistory.Add(
			new(
				_timeControl.Value.InitialTime,
				_timeControl.Value.InitialTime,
				activeColor,
				0,
				0,
				0,
				DateTimeOffset.UtcNow,
				null,
				TimeSpan.Zero
			)
		);
	}

	private void AdvanceClockForCompletedMove(char movingSide, DateTimeOffset? completedAtUtc = null)
	{
		if (!_timeControl.HasValue || _clockHistory.Count == 0)
			return;

		var checkpoint = _clockHistory[^1];
		var now = completedAtUtc ?? DateTimeOffset.UtcNow;
		var elapsed = ComputeElapsed(checkpoint, now);
		var whiteRemaining = checkpoint.WhiteRemaining;
		var blackRemaining = checkpoint.BlackRemaining;
		var whiteMoves = checkpoint.WhiteMovesCompleted;
		var blackMoves = checkpoint.BlackMovesCompleted;

		var stage = GetStageForSide(checkpoint, movingSide);
		var delay = stage.DelayPerMove;
		var mainElapsed = elapsed > delay ? elapsed - delay : TimeSpan.Zero;

		if (movingSide == 'w')
		{
			whiteRemaining = whiteRemaining - mainElapsed;
			if (whiteRemaining > TimeSpan.Zero)
			{
				whiteMoves++;
				whiteRemaining += stage.IncrementPerMove;
				whiteRemaining += GetAddedStageTime(whiteMoves);
			}
			else
				whiteRemaining = TimeSpan.Zero;
		}
		else
		{
			blackRemaining = blackRemaining - mainElapsed;
			if (blackRemaining > TimeSpan.Zero)
			{
				blackMoves++;
				blackRemaining += stage.IncrementPerMove;
				blackRemaining += GetAddedStageTime(blackMoves);
			}
			else
				blackRemaining = TimeSpan.Zero;
		}

		_clockHistory.Add(
			new(
				whiteRemaining,
				blackRemaining,
				Opposite(movingSide),
				whiteMoves,
				blackMoves,
				GetStageIndexForSide(Opposite(movingSide) == 'w' ? whiteMoves : blackMoves),
				now,
				null,
				TimeSpan.Zero
			)
		);
	}

	private PlayableMatchClockState? GetClockSnapshot(DateTimeOffset? snapshotUtc = null)
	{
		if (!_timeControl.HasValue || _clockHistory.Count == 0)
			return null;

		var checkpoint = _clockHistory[^1];
		var now = snapshotUtc ?? DateTimeOffset.UtcNow;
		var elapsed = ComputeElapsed(checkpoint, now);
		var whiteRemaining = checkpoint.WhiteRemaining;
		var blackRemaining = checkpoint.BlackRemaining;
		var stage = GetStageForSide(checkpoint, checkpoint.ActiveColor);
		var delayRemaining = elapsed < stage.DelayPerMove ? stage.DelayPerMove - elapsed : TimeSpan.Zero;
		var mainElapsed = elapsed > stage.DelayPerMove ? elapsed - stage.DelayPerMove : TimeSpan.Zero;

		if (checkpoint.ActiveColor == 'w')
			whiteRemaining = ClampToZero(whiteRemaining - mainElapsed);
		else
			blackRemaining = ClampToZero(blackRemaining - mainElapsed);

		return new(
			whiteRemaining,
			blackRemaining,
			checkpoint.ActiveColor,
			delayRemaining,
			_isClockPaused,
			checkpoint.ActiveStageIndex,
			now
		);
	}

	private void EnsureTurnHasTimeRemaining(DateTimeOffset? snapshotUtc = null)
	{
		if (_timeControl.HasValue && _timeControl.Value.TimeoutPolicy == PlayableMatchTimeoutPolicy.Ignore)
			return;

		var clock = GetClockSnapshot(snapshotUtc);
		if (!clock.HasValue)
			return;

		var currentRemaining = clock.Value.ActiveColor == 'w'
			? clock.Value.WhiteRemaining
			: clock.Value.BlackRemaining;
		if (currentRemaining > TimeSpan.Zero)
			return;

		throw new InvalidOperationException("The current side has already lost on time.");
	}

	private MatchOutcome EvaluateOutcome(
		Fen                    fen,
		IReadOnlyList<string>  legalMoves,
		PlayableMatchClockState? clock)
	{
		if (_forcedResult.IsTerminal)
			return new(_forcedResult, null);

		if (clock.HasValue)
		{
			var activeRemaining = fen.ActiveColor == 'w'
				? clock.Value.WhiteRemaining
				: clock.Value.BlackRemaining;
			if (_timeControl.HasValue &&
				_timeControl.Value.TimeoutPolicy == PlayableMatchTimeoutPolicy.AutomaticLoss &&
				activeRemaining <= TimeSpan.Zero)
			{
				return new(new(PlayableMatchResultReason.Timeout, Opposite(fen.ActiveColor)), null);
			}
		}

		if (legalMoves.Count == 0)
		{
			return LocalFenRules.IsCurrentPlayerInCheck(fen)
				? new(new(PlayableMatchResultReason.Checkmate, Opposite(fen.ActiveColor)), null)
				: new(new(PlayableMatchResultReason.Stalemate, null), null);
		}

		if (fen.HalfmoveClock >= 100)
			return CreateClaimableOrAutomaticResult(PlayableMatchResultReason.FiftyMoveRule);

		if (LocalFenRules.HasInsufficientMaterial(fen))
			return new(new(PlayableMatchResultReason.InsufficientMaterial, null), null);

		if (CountRepetitions(fen) >= 3)
			return CreateClaimableOrAutomaticResult(PlayableMatchResultReason.ThreefoldRepetition);

		return new(default, null);
	}

	private int CountRepetitions(Fen currentFen)
	{
		string currentKey = LocalFenRules.BuildRepetitionKey(currentFen);
		var count = LocalFenRules.BuildRepetitionKey(_state.BaseFen) == currentKey ? 1 : 0;
		foreach (var move in _appliedMoveHistory)
		{
			if (LocalFenRules.BuildRepetitionKey(move.ResultingFen) == currentKey)
				count++;
		}

		return count;
	}

	private MatchOutcome CreateClaimableOrAutomaticResult(PlayableMatchResultReason reason)
	{
		var drawResult = new PlayableMatchResult(reason, null);
		return _claimableDrawPolicy == PlayableMatchClaimableDrawPolicy.Automatic
			? new(drawResult, null)
			: new(default, drawResult);
	}

	private static TimeSpan ClampToZero(TimeSpan remaining) =>
		remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;

	private TimeSpan ComputeElapsed(ClockCheckpoint checkpoint, DateTimeOffset now)
	{
		var effectiveNow = checkpoint.PausedAtUtc ?? now;
		var elapsed = effectiveNow - checkpoint.TurnStartedAtUtc - checkpoint.PausedAccumulated;
		return elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed;
	}

	private StageSettings GetStageForSide(ClockCheckpoint checkpoint, char side)
	{
		var movesCompleted = side == 'w' ? checkpoint.WhiteMovesCompleted : checkpoint.BlackMovesCompleted;
		var stageIndex = GetStageIndexForSide(movesCompleted);
		if (!_timeControl.HasValue || stageIndex == 0)
		{
			return new(
				_timeControl?.IncrementPerMove ?? TimeSpan.Zero,
				_timeControl?.DelayPerMove ?? TimeSpan.Zero
			);
		}

		var stage = _timeControl.Value.AdditionalStages[stageIndex - 1];
		return new(stage.IncrementPerMove, stage.DelayPerMove);
	}

	private int GetStageIndexForSide(int movesCompleted)
	{
		if (!_timeControl.HasValue || _timeControl.Value.AdditionalStages.IsDefaultOrEmpty)
			return 0;

		var index = 0;
		for (var i = 0; i < _timeControl.Value.AdditionalStages.Length; i++)
		{
			if (movesCompleted >= _timeControl.Value.AdditionalStages[i].TriggerMovesPerSide)
				index = i + 1;
		}

		return index;
	}

	private TimeSpan GetAddedStageTime(int movesCompleted)
	{
		if (!_timeControl.HasValue || _timeControl.Value.AdditionalStages.IsDefaultOrEmpty)
			return TimeSpan.Zero;

		foreach (var stage in _timeControl.Value.AdditionalStages)
		{
			if (movesCompleted == stage.TriggerMovesPerSide)
				return stage.AddedTime;
		}

		return TimeSpan.Zero;
	}

	private static char Opposite(char color) => color == 'w' ? 'b' : 'w';

	private async Task RollbackStartAsync(bool snapshotStarted, bool ponderStarted, bool classifierStarted)
	{
		if (classifierStarted)
			try
			{
				await _classificationClient.StopAsync(CancellationToken.None).ConfigureAwait(false);
			}
			catch
			{
				/* best-effort */
			}

		if (ponderStarted)
			try
			{
				await _ponder.StopAsync(CancellationToken.None).ConfigureAwait(false);
			}
			catch
			{
				/* best-effort */
			}

		if (snapshotStarted)
			try
			{
				await _snapshotClient.StopAsync(CancellationToken.None).ConfigureAwait(false);
			}
			catch
			{
				/* best-effort */
			}

		ClearState();
		Interlocked.Exchange(ref _acceptedPonderGeneration, 0);
		lock (_sync)
		{
			_state = UciState.Default;
		}
	}

	private void SetSearchingState(bool isSearching)
	{
		UciState snapshot;
		lock (_sync)
		{
			if (_state.IsSearching == isSearching) return;

			_state    = _state with { IsSearching = isSearching };
			snapshot = _state;
		}

		PublishSearchStateChanged(snapshot);
		if (isSearching)
			RaiseStateEvent(EngineThinkingStarted, UciGameEngineSessionEventKind.EngineThinkingStarted, snapshot);
		else
			RaiseStateEvent(EngineThinkingStopped, UciGameEngineSessionEventKind.EngineThinkingStopped, snapshot);
	}

	private void AcceptPonderGeneration(int generation) =>
		Interlocked.Exchange(ref _acceptedPonderGeneration, generation);

	internal int AcceptedPonderGenerationForTests => Volatile.Read(ref _acceptedPonderGeneration);
	internal int AcceptedClassificationGenerationForTests
	{
		get
		{
			return _classificationRuntime.Generation;
		}
	}

	internal void ApplyPonderBestMoveForTests(ParsedMove best, ParsedMove? ponder, int generation) =>
		ApplyPonderBestMove(best, ponder, generation);

	internal void ApplyPonderInfoForTests(PrincipalVariation pv, int generation) =>
		ApplyPonderInfo(pv, generation);

	internal void ApplyClassifiedMoveForTests(Move move, int generation) =>
		ApplyClassifiedMove(move, generation);

	internal void CompleteClassificationForTests(int generation) =>
		CompleteClassificationRun(generation);

	private void InvalidatePonderState(bool clearTransientState)
	{
		Interlocked.Exchange(ref _acceptedPonderGeneration, 0);

		UciState snapshot;
		bool     changed = false;
		lock (_sync)
		{
			var next = _state with { IsSearching = false };
			if (clearTransientState)
				next = next with
				{
					Evaluation = null,
					BestMove   = null,
					PonderMove = null
				};

			if (next == _state) return;

			_state   = next;
			snapshot = _state;
			changed  = true;
		}

		if (changed)
			PublishSearchStateChanged(snapshot);
	}

	private int BeginClassificationRun() => _classificationRuntime.BeginRun();

	private void ApplyClassifiedMove(Move move, int generation)
	{
		_classificationRuntime.ApplyClassifiedMove(ref _state, move, generation, out var snapshot);
		if (!snapshot.HasValue)
			return;

		RaiseStateEvent(StateChanged, UciGameEngineSessionEventKind.StateChanged, snapshot.Value);
		RaiseTypedEvent(MoveClassified, move, UciGameEngineSessionEvent.ForClassifiedMove(move));
#pragma warning disable CS0618
		_events.Raise(MoveClassificationUpdated, move);
#pragma warning restore CS0618
	}

	private void CompleteClassificationRun(int generation)
	{
		_classificationRuntime.CompleteRun(State, generation, out var snapshot);
		if (snapshot.HasValue)
			RaiseStateEvent(
				ClassificationCompleted,
				UciGameEngineSessionEventKind.ClassificationCompleted,
				snapshot.Value
			);
	}

	private void FaultClassificationRun(int generation, Exception ex) =>
		_classificationRuntime.FaultRun(generation, ex);

	private void CancelClassificationRunIfActive(int generation) =>
		_classificationRuntime.CancelRunIfActive(generation);

	private void CancelClassificationCompletion() =>
		_classificationRuntime.CancelCompletion();

	/// <summary>
	///     Forwards ponder engine PV updates.
	/// </summary>
	private void OnPonderInfo(PrincipalVariation pv, int generation) =>
		ApplyPonderInfo(pv, generation);

	private void ApplyPonderInfo(PrincipalVariation pv, int generation)
	{
		if (generation != Volatile.Read(ref _acceptedPonderGeneration)) return;

		UciState snapshot;
		lock (_sync)
		{
			_state   = _state with { Evaluation = pv };
			snapshot = _state;
		}

		RaiseStateEvent(StateChanged, UciGameEngineSessionEventKind.StateChanged, snapshot);
		RaiseTypedEvent(EvaluationChanged, pv, UciGameEngineSessionEvent.ForEvaluation(pv));
#pragma warning disable CS0618
		_events.Raise(EvaluationUpdated, pv);
#pragma warning restore CS0618
	}

	/// <summary>
	///     Forwards the best move (and optional ponder move) found by the ponder engine.
	/// </summary>
	private void PonderOnBestMove(string best, string? ponder, int generation) =>
		ApplyPonderBestMove(
			ParsedMove.FromNotation(best),
			string.IsNullOrWhiteSpace(ponder) ? null : ParsedMove.FromNotation(ponder),
			generation
		);

	private void ApplyPonderBestMove(ParsedMove best, ParsedMove? ponder, int generation)
	{
		if (generation != Volatile.Read(ref _acceptedPonderGeneration)) return;

		UciState snapshot;
		lock (_sync)
		{
			_state   = _state with { BestMove = best, PonderMove = ponder };
			snapshot = _state;
		}

		RaiseStateEvent(StateChanged, UciGameEngineSessionEventKind.StateChanged, snapshot);
		RaiseStateEvent(BestMoveChanged, UciGameEngineSessionEventKind.BestMoveChanged, snapshot);
	}

	private void RaiseSessionEvent(Action? legacyHandler, UciGameEngineSessionEventKind kind)
	{
		_events.Raise(legacyHandler);
		_events.Raise(EventPublished, UciGameEngineSessionEvent.Create(kind));
	}

	private void RaiseStateEvent(
		Action<UciState>?              legacyHandler,
		UciGameEngineSessionEventKind  kind,
		UciState                       state)
	{
		_events.Raise(legacyHandler, state);
		_events.Raise(EventPublished, UciGameEngineSessionEvent.ForState(kind, state));
	}

	private void RaiseTypedEvent<T>(
		Action<T>?                     legacyHandler,
		T                              payload,
		UciGameEngineSessionEvent      sessionEvent)
	{
		_events.Raise(legacyHandler, payload);
		_events.Raise(EventPublished, sessionEvent);
	}

	private void PublishPositionChanged(UciState snapshot)
	{
		RaiseStateEvent(StateChanged, UciGameEngineSessionEventKind.StateChanged, snapshot);
		RaiseStateEvent(PositionChanged, UciGameEngineSessionEventKind.PositionChanged, snapshot);
	}

	private void PublishGameOver(UciState snapshot)
	{
		if (snapshot.IsGameOver)
			RaiseStateEvent(GameOver, UciGameEngineSessionEventKind.GameOver, snapshot);
	}

	private void PublishSearchStateChanged(UciState snapshot)
	{
		RaiseStateEvent(StateChanged, UciGameEngineSessionEventKind.StateChanged, snapshot);
		RaiseStateEvent(SearchStateChanged, UciGameEngineSessionEventKind.SearchStateChanged, snapshot);
	}

	/// <summary>
	///     Raises the Error event with the specified exception.
	/// </summary>
	private void RaiseError(Exception ex)
	{
		RaiseTypedEvent(Error, ex, UciGameEngineSessionEvent.ForError(ex));
#pragma warning disable CS0618
		_events.Raise(EngineError, ex);
#pragma warning restore CS0618
	}

	private async Task StopSearchCoreAsync(CancellationToken ct)
	{
		InvalidatePonderState(clearTransientState: true);
		CoordinatorRuntimeUtilities.CancelAndDispose(ref _bestCts);

		try
		{
			await _ponder.StopSearchAsync(ct).ConfigureAwait(false);
		}
		finally
		{
			SetSearchingState(false);
		}
	}

	private async Task<bool> TryStopStepAsync(
		Func<CancellationToken, Task> step,
		bool                          cancellationRequested,
		List<Exception>               failures,
		CancellationToken             callerToken,
		bool                          reportErrors = true)
	{
		try
		{
			await step(cancellationRequested ? CancellationToken.None : callerToken).ConfigureAwait(false);
			return cancellationRequested;
		}
		catch (OperationCanceledException) when (callerToken.IsCancellationRequested)
		{
			return true;
		}
		catch (OperationCanceledException)
		{
			return cancellationRequested;
		}
		catch (Exception ex)
		{
			failures.Add(ex);
			if (reportErrors)
				RaiseError(ex);

			return cancellationRequested;
		}
	}

	private readonly record struct ClockCheckpoint(
		TimeSpan        WhiteRemaining,
		TimeSpan        BlackRemaining,
		char            ActiveColor,
		int             WhiteMovesCompleted,
		int             BlackMovesCompleted,
		int             ActiveStageIndex,
		DateTimeOffset  TurnStartedAtUtc,
		DateTimeOffset? PausedAtUtc,
		TimeSpan        PausedAccumulated
	);

	private readonly record struct MatchOutcome(
		PlayableMatchResult  Result,
		PlayableMatchResult? ClaimableResult
	);

	private readonly record struct StageSettings(
		TimeSpan IncrementPerMove,
		TimeSpan DelayPerMove
	);

	private readonly record struct MetadataUpdate(
		UciState Snapshot,
		bool     ResultChanged
	);
}
