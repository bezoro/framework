using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Bezoro.Chess.UCI.API.Types;
using Bezoro.Chess.UCI.Domain.Engines;

namespace Bezoro.Chess.UCI.API;

/// <summary>
///     Coordinates multiple UCI engines for fast information, pondering, and full move classification.
///     Orchestrates updating positions, synchronized pondering, background classification, and emits unified events
///     updating UI or consumers.
/// </summary>
public sealed class UciCoordinator : IAsyncDisposable, IDisposable
{
	private readonly MoveClassificationEngine _classifier;
	private readonly object                   _sync = new();
	private readonly PonderEngine             _ponder;
	private readonly QuickInfoEngine          _quick;
	private readonly SynchronizationContext?  _syncContext;

	private readonly UciCoordinatorOptions _options;

	private CancellationTokenSource? _bestCts;
	private CancellationTokenSource? _classificationCts;
	private TaskCompletionSource<IReadOnlyDictionary<string, Move>>? _classificationCompletion;
	private int                      _acceptedPonderGeneration;
	private int                      _classificationGeneration;

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
	///     Raised when the coordinator state changes.
	/// </summary>
	public event Action<UciState>? StateChanged;

	/// <summary>
	///     Raised when the coordinator has fully stopped.
	/// </summary>
	public event Action? Stopped;

	/// <summary>
	///     Constructs a new UciCoordinator with engines initialized for the given enginePath, arguments, and working
	///     directory.
	/// </summary>
	/// <param name="enginePath">Path to the UCI engine executable.</param>
	/// <param name="args">Optional arguments for the engine.</param>
	/// <param name="workingDirectory">Optional working directory.</param>
	/// <param name="syncContext">Optional synchronization context to marshal events to (e.g. UI thread).</param>
	/// <param name="options">Optional configuration options for the coordinator.</param>
	public UciCoordinator(
		string                  enginePath,
		IEnumerable<string>?    args             = null,
		string?                 workingDirectory = null,
		SynchronizationContext? syncContext      = null,
		UciCoordinatorOptions?  options          = null)
		: this(
			new QuickInfoEngine(enginePath, args, workingDirectory),
			new PonderEngine(enginePath, args, workingDirectory),
			new MoveClassificationEngine(enginePath, args, workingDirectory),
			syncContext,
			options
		) { }

	internal UciCoordinator(
		QuickInfoEngine         quick,
		PonderEngine            ponder,
		MoveClassificationEngine classifier,
		SynchronizationContext? syncContext = null,
		UciCoordinatorOptions?  options     = null)
	{
		_syncContext = syncContext;
		_options     = options ?? UciCoordinatorOptions.Default;
		_quick       = quick ?? throw new ArgumentNullException(nameof(quick));
		_ponder      = ponder ?? throw new ArgumentNullException(nameof(ponder));
		_classifier  = classifier ?? throw new ArgumentNullException(nameof(classifier));

		_ponder.InfoPvWithGeneration   += OnPonderInfo;
		_ponder.BestMoveWithGeneration += PonderOnBestMove;
	}

	/// <summary>
	///     Gets a value indicating whether all underlying engines are healthy and responsive.
	/// </summary>
	public bool IsHealthy => _quick.IsHealthy && _ponder.IsHealthy && _classifier.IsHealthy;

	/// <summary>
	///     Gets a value indicating whether all underlying engines have been started.
	/// </summary>
	public bool IsStarted => _quick.IsStarted && _ponder.IsStarted && _classifier.IsStarted;

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
	///     Gets the engine metadata reported by the quick engine instance.
	/// </summary>
	public UciEngineInfo EngineInfo => _quick.EngineInfo;

	/// <summary>
	///     Gets the options advertised by the quick engine instance during handshake.
	/// </summary>
	public ImmutableArray<UciEngineOption> AvailableOptions => _quick.AvailableOptions;

	/// <summary>
	///     Gets the capability state detected for the configured engine.
	/// </summary>
	public UciEngineCapabilities Capabilities => _quick.Capabilities;

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

			Raise(StateChanged, value);
		}
	}

	/// <summary>
	///     Creates and starts a new UciCoordinator with engines initialized and ready.
	/// </summary>
	/// <param name="enginePath">Path to the UCI engine executable.</param>
	/// <param name="options">Configuration options for the coordinator.</param>
	/// <param name="syncContext">Optional synchronization context to marshal events to (e.g. UI thread).</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>A fully initialized and started coordinator.</returns>
	public static async Task<UciCoordinator> CreateAsync(
		string                  enginePath,
		UciCoordinatorOptions?  options     = null,
		SynchronizationContext? syncContext = null,
		CancellationToken       ct          = default)
	{
		var coordinator = new UciCoordinator(enginePath, null, null, syncContext, options);
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
			completionTask = _classificationCompletion?.Task;
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
		CancelAndDispose(ref _bestCts);
		ClearState();
		await Task.WhenAll(
			_quick.NewGameAsync(ct),
			_ponder.NewGameAsync(ct),
			_classifier.NewGameAsync(ct)
		).ConfigureAwait(false);

		Interlocked.Exchange(ref _acceptedPonderGeneration, 0);
		State = UciState.Default;
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
			_quick.SetOptionAsync(name, value, ct),
			_ponder.SetOptionAsync(name, value, ct),
			_classifier.SetOptionAsync(name, value, ct)
		);

	/// <summary>
	///     Sends the standard UCI <c>debug on/off</c> command to all internal engines.
	/// </summary>
	public Task SetDebugAsync(bool enabled, CancellationToken ct = default) =>
		Task.WhenAll(
			_quick.SetDebugAsync(enabled, ct),
			_ponder.SetDebugAsync(enabled, ct),
			_classifier.SetDebugAsync(enabled, ct)
		);

	/// <summary>
	///     Sends the standard UCI <c>register</c> command to all internal engines.
	/// </summary>
	public Task RegisterAsync(UciRegistration registration, CancellationToken ct = default) =>
		Task.WhenAll(
			_quick.RegisterAsync(registration, ct),
			_ponder.RegisterAsync(registration, ct),
			_classifier.RegisterAsync(registration, ct)
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
		var quickStarted      = false;
		var ponderStarted     = false;
		var classifierStarted = false;

		try
		{
			await _quick.StartAsync(ct).ConfigureAwait(false);
			quickStarted = true;
			EnsureCoordinatorCapabilities(_quick.Capabilities);

			await _ponder.StartAsync(ct).ConfigureAwait(false);
			ponderStarted = true;

			await _classifier.StartAsync(ct).ConfigureAwait(false);
			classifierStarted = true;

			// Configure ponder engine based on options.
			await _ponder.SetOptionAsync("Threads", _options.PonderThreads.ToString(), ct).ConfigureAwait(false);
			await _ponder.SetOptionAsync("MultiPv", _options.MultiPv.ToString(),       ct).ConfigureAwait(false);

			ClearState();
			State = UciState.Default;

			Raise(Ready);
		}
		catch
		{
			await RollbackStartAsync(quickStarted, ponderStarted, classifierStarted).ConfigureAwait(false);
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
		CancelAndDispose(ref _bestCts, newCts);

		try
		{
			await _ponder.StartSearchAsync(fen, playedMoves, token).ConfigureAwait(false);
			AcceptPonderGeneration(_ponder.CurrentSearchGeneration);
			SetSearchingState(true);
		}
		catch
		{
			if (CancelAndDisposeIfCurrent(ref _bestCts, newCts))
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
			token => _classifier.StopAsync(token),
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
			token => _quick.StopAsync(token),
			cancellationRequested,
			stopFailures,
			ct
		).ConfigureAwait(false);

		Interlocked.Exchange(ref _acceptedPonderGeneration, 0);
		State = UciState.Default;

		Raise(Stopped);
		ThrowStopFailures(stopFailures, cancellationRequested, ct);
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
		// Stop previous searches and any ongoing classification
		await StopSearchAsync(ct).ConfigureAwait(false);
		ClearState();

		var movesList = playedMoves?.ToList() ?? new List<string>();

		// Set the position and publish legal moves
		await _quick.SetPositionAsync(fen, movesList, ct).ConfigureAwait(false);
		// Keep ponder engine synchronized with the quick engine position
		await _ponder.SetPositionAsync(fen, movesList, ct).ConfigureAwait(false);

		var legalMoves = await _quick.GetLegalMovesAsync(ct).ConfigureAwait(false);

		// Get the effective FEN (actual position after moves)
		var effectiveFen = await _quick.GetCurrentFenAsync(ct).ConfigureAwait(false);

		lock (_sync)
		{
			_state = new(
				fen,
				effectiveFen ?? fen,
				movesList.ToImmutableList(),
				legalMoves.ToImmutableList(),
				ImmutableDictionary<string, Move>.Empty,
				null,
				null,
				null,
				false
			);
		}

		Raise(StateChanged, _state);

		// Start pondering for the new position
		try
		{
			await StartSearchAsync(fen, movesList, ct).ConfigureAwait(false);
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

		// Start background classification (events will propagate results)
		if (effectiveFen is { } currentFen)
		{
			var classificationCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
			var classificationToken = classificationCts.Token;
			CancelAndDispose(ref _classificationCts, classificationCts);

			_ = Task.Run(
				async () =>
				{
					try
					{
						await foreach (var move in _classifier
												.ClassifyAsync(
													currentFen,
													_options.ClassificationDepth,
													legalMoves,
													classificationToken
												)
												.ConfigureAwait(false))
						{
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
		else
		{
			CompleteClassificationRun(classificationGeneration);
		}
	}

	/// <summary>
	///     Gets the actual current FEN (after all played moves) as seen by the engine.
	/// </summary>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>The current position FEN, or null if not available.</returns>
	public Task<Fen?> GetCurrentFenAsync(CancellationToken ct = default) =>
		_quick.GetCurrentFenAsync(ct);

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

			waitTask = _classificationCompletion?.Task ??
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

		// Use the classifier to classify this specific move
		var result = await _classifier.ClassifyMoveAsync(currentState.CurrentFen, move, _options.ClassificationDepth, ct)
									  .ConfigureAwait(false);

		if (!result.HasValue)
			throw new InvalidOperationException($"Failed to classify move '{move}'.");

		return result.Value;
	}

	/// <summary>
	///     Performs a blocking search with the specified parameters and returns the result.
	/// </summary>
	/// <param name="parameters">Search parameters (depth, time, nodes, etc.).</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>The search result containing the best move and evaluation.</returns>
	public Task<SearchResult> SearchAsync(SearchParameters parameters, CancellationToken ct = default) =>
		_quick.SearchAsync(parameters, CurrentFen, null, ct);

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
		UciState currentState;
		lock (_sync)
		{
			currentState = _state;
		}

		if (!currentState.LegalMoves.Contains(move))
			throw new ArgumentException($"Move '{move}' is not legal in the current position.", nameof(move));

		var newMoves = new List<string>(currentState.PlayedMoves) { move };
		await UpdatePositionAsync(currentState.BaseFen, newMoves, ct).ConfigureAwait(false);
		return State;
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
		lock (_sync)
		{
			currentState = _state;
		}

		if (currentState.PlayedMoves.Count == 0)
			return currentState;

		int movesToRemove = Math.Min(count, currentState.PlayedMoves.Count);
		var newMoves      = currentState.PlayedMoves.Take(currentState.PlayedMoves.Count - movesToRemove).ToList();

		await UpdatePositionAsync(currentState.BaseFen, newMoves, ct).ConfigureAwait(false);
		return State;
	}

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
		CancelAndDispose(ref _bestCts);

		await _classifier.DisposeAsync();
		await _ponder.DisposeAsync();
		await _quick.DisposeAsync();
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
	///     Atomically cancels and disposes a CancellationTokenSource field, optionally replacing it.
	/// </summary>
	private static void CancelAndDispose(
		ref CancellationTokenSource? ctsField,
		CancellationTokenSource?     replacement = null)
	{
		var old = Interlocked.Exchange(ref ctsField, replacement);
		try
		{
			old?.Cancel();
		}
		catch
		{
			// Best-effort: ignore errors when canceling
		}
		finally
		{
			old?.Dispose();
		}
	}

	private static bool CancelAndDisposeIfCurrent(
		ref CancellationTokenSource? ctsField,
		CancellationTokenSource      candidate)
	{
		var current = Interlocked.CompareExchange(ref ctsField, null, candidate);
		if (!ReferenceEquals(current, candidate)) return false;

		try
		{
			candidate.Cancel();
		}
		catch
		{
			// Best-effort: ignore errors when canceling
		}
		finally
		{
			candidate.Dispose();
		}

		return true;
	}

	/// <summary>
	///     Cancels background move classification and clears all cached per-move results.
	/// </summary>
	private void ClearState()
	{
		_classifier.StopClassification();
		CancelAndDispose(ref _classificationCts);
		CancelClassificationCompletion();
	}

	private async Task RollbackStartAsync(bool quickStarted, bool ponderStarted, bool classifierStarted)
	{
		if (classifierStarted)
			try
			{
				await _classifier.StopAsync(CancellationToken.None).ConfigureAwait(false);
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

		if (quickStarted)
			try
			{
				await _quick.StopAsync(CancellationToken.None).ConfigureAwait(false);
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

		Raise(StateChanged, snapshot);
	}

	private void AcceptPonderGeneration(int generation) =>
		Interlocked.Exchange(ref _acceptedPonderGeneration, generation);

	internal int AcceptedPonderGenerationForTests => Volatile.Read(ref _acceptedPonderGeneration);
	internal int AcceptedClassificationGenerationForTests
	{
		get
		{
			lock (_sync)
			{
				return _classificationGeneration;
			}
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
			Raise(StateChanged, snapshot);
	}

	private int BeginClassificationRun()
	{
		lock (_sync)
		{
			_classificationGeneration++;
			_classificationCompletion = CreateClassificationCompletionSource();
			return _classificationGeneration;
		}
	}

	private void ApplyClassifiedMove(Move move, int generation)
	{
		UciState snapshot;
		lock (_sync)
		{
			if (_classificationGeneration != generation) return;
			if (!_state.LegalMoves.Contains(move.Notation)) return;

			var newClassified = _state.ClassifiedMoves.SetItem(move.Notation, move);
			_state = _state with { ClassifiedMoves = newClassified };
			snapshot = _state;
		}

		Raise(StateChanged, snapshot);
	}

	private void CompleteClassificationRun(int generation)
	{
		TaskCompletionSource<IReadOnlyDictionary<string, Move>>? completion;
		IReadOnlyDictionary<string, Move> result;
		lock (_sync)
		{
			if (_classificationGeneration != generation) return;

			completion = _classificationCompletion;
			result     = _state.ClassifiedMoves;
		}

		completion?.TrySetResult(result);
	}

	private void FaultClassificationRun(int generation, Exception ex)
	{
		TaskCompletionSource<IReadOnlyDictionary<string, Move>>? completion;
		lock (_sync)
		{
			if (_classificationGeneration != generation) return;

			completion = _classificationCompletion;
		}

		completion?.TrySetException(ex);
	}

	private void CancelClassificationRunIfActive(int generation)
	{
		TaskCompletionSource<IReadOnlyDictionary<string, Move>>? completion;
		lock (_sync)
		{
			if (_classificationGeneration != generation) return;

			completion = _classificationCompletion;
		}

		completion?.TrySetCanceled();
	}

	private void CancelClassificationCompletion()
	{
		TaskCompletionSource<IReadOnlyDictionary<string, Move>>? completion;
		lock (_sync)
		{
			_classificationGeneration++;
			completion = _classificationCompletion;
			_classificationCompletion = null;
		}

		completion?.TrySetCanceled();
	}

	private static TaskCompletionSource<IReadOnlyDictionary<string, Move>> CreateClassificationCompletionSource() =>
		new(TaskCreationOptions.RunContinuationsAsynchronously);

	private static void EnsureCoordinatorCapabilities(UciEngineCapabilities capabilities)
	{
		if (capabilities.SupportsCoordinatorExtensions) return;

		throw new NotSupportedException(
			$"UciCoordinator requires engine support for display-board FEN retrieval and perft move listing. " +
			$"Detected capabilities: DisplayBoardFen={capabilities.DisplayBoardFen}, " +
			$"PerftMoveListing={capabilities.PerftMoveListing}."
		);
	}

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

		Raise(StateChanged, snapshot);
	}

	/// <summary>
	///     Forwards the best move (and optional ponder move) found by the ponder engine.
	/// </summary>
	private void PonderOnBestMove(ParsedMove best, ParsedMove? ponder, int generation) =>
		ApplyPonderBestMove(best, ponder, generation);

	private void ApplyPonderBestMove(ParsedMove best, ParsedMove? ponder, int generation)
	{
		if (generation != Volatile.Read(ref _acceptedPonderGeneration)) return;

		UciState snapshot;
		lock (_sync)
		{
			_state   = _state with { BestMove = best, PonderMove = ponder };
			snapshot = _state;
		}

		Raise(StateChanged, snapshot);
	}

	private void Raise(Action? handler)
	{
		if (handler == null) return;

		if (_syncContext != null)
			_syncContext.Post(_ => handler(), null);
		else
			handler();
	}

	private void Raise<T>(Action<T>? handler, T args)
	{
		if (handler == null) return;

		if (_syncContext != null)
			_syncContext.Post(_ => handler(args), null);
		else
			handler(args);
	}

	/// <summary>
	///     Raises the Error event with the specified exception.
	/// </summary>
	private void RaiseError(Exception ex) => Raise(Error, ex);

	private static void ThrowStopFailures(
		IReadOnlyList<Exception> failures,
		bool                     cancellationRequested,
		CancellationToken        ct)
	{
		if (failures.Count == 0)
		{
			if (cancellationRequested)
				throw new OperationCanceledException(ct);

			return;
		}

		if (failures.Count == 1)
			throw failures[0];

		throw new AggregateException(failures);
	}

	private async Task StopSearchCoreAsync(CancellationToken ct)
	{
		InvalidatePonderState(clearTransientState: true);
		CancelAndDispose(ref _bestCts);

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
}
