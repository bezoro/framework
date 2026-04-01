using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.Chess.UCI.Protocol.API.Common.Extensions;
using Bezoro.Chess.UCI.Protocol.API.Types;
using Bezoro.Chess.UCI.Protocol.Internal;

namespace Bezoro.Chess.UCI.Protocol.API;

/// <summary>
///     Coordinates a playable match using separate playing, snapshot, and full-strength analysis clients, plus local
///     board-state ownership for legal moves, promotion flow, clocks, and result adjudication.
/// </summary>
public sealed class UciPlayableMatchSession
{
	private readonly MoveClassificationCoordinator  _classifications = new();
	private readonly List<ClockCheckpoint>          _clockHistory = [];
	private readonly int                            _engineMoveTimeMs;
	private readonly List<PlayedMove>               _moveHistory = [];
	private readonly List<string>                   _playedMoves = [];
	private readonly UciEngineClient                _analysisClient;
	private readonly UciEngineClient                _moveListClient;
	private readonly UciEngineClient                _playingClient;
	private readonly UciPositionAnalysisCoordinator _positionAnalysis;
	private readonly PlayableMatchClaimableDrawPolicy _claimableDrawPolicy;
	private readonly PlayableMatchControlledMoveFallbackPolicy _controlledMoveFallbackPolicy;
	private readonly PlayableMatchDrawOfferPolicy _drawOfferPolicy;
	private readonly PlayableMatchTimeControl?      _timeControl;
	private readonly Func<DateTimeOffset>           _utcNowProvider;
	private          bool                           _hasCurrentState;
	private          Fen                            _baseFen = Fen.Default;
	private          PlayableMatchState             _currentState;
	private          PlayableMatchResult?           _claimableResult;
	private          char?                          _drawOfferedBy;
	private          PlayableMatchResult            _forcedResult;
	private          bool                           _isClockPaused;
	private          PlayableMatchResult            _lastResult;
	private          PendingPromotionRequest?       _pendingPromotion;

	/// <summary>
	///     Occurs whenever the session emits a canonical protocol-side event.
	/// </summary>
	public event Action<PlayableMatchEvent>? EventOccurred;

	/// <summary>
	///     Creates a playable match session.
	/// </summary>
	public UciPlayableMatchSession(
		UciEngineClient            playingClient,
		UciEngineClient            analysisClient,
		UciEngineClient            moveListClient,
		char                       perspectiveColor,
		MatchSideControllerKind    whiteController,
		MatchSideControllerKind    blackController,
		int                        engineMoveTimeMs       = 1_000,
		int                        moveListAnalysisTimeMs = 3_000,
		int                        moveListFallbackTimeMs = 250,
		PlayableMatchClaimableDrawPolicy claimableDrawPolicy = PlayableMatchClaimableDrawPolicy.Automatic,
		PlayableMatchDrawOfferPolicy drawOfferPolicy = PlayableMatchDrawOfferPolicy.ExpireOnMove,
		PlayableMatchControlledMoveFallbackPolicy controlledMoveFallbackPolicy = PlayableMatchControlledMoveFallbackPolicy.UseLocalFallback,
		PlayableMatchTimeControl?  timeControl            = null,
		Func<DateTimeOffset>?      utcNowProvider         = null)
	{
		_playingClient    = playingClient ?? throw new ArgumentNullException(nameof(playingClient));
		_analysisClient   = analysisClient ?? throw new ArgumentNullException(nameof(analysisClient));
		_moveListClient   = moveListClient ?? throw new ArgumentNullException(nameof(moveListClient));
		_engineMoveTimeMs = ValidatePositive(engineMoveTimeMs, nameof(engineMoveTimeMs));
		PerspectiveColor  = NormalizeColor(perspectiveColor, nameof(perspectiveColor));
		WhiteController   = whiteController;
		BlackController   = blackController;
		_utcNowProvider   = utcNowProvider ?? (() => DateTimeOffset.UtcNow);
		_claimableDrawPolicy = claimableDrawPolicy;
		_drawOfferPolicy = drawOfferPolicy;
		_controlledMoveFallbackPolicy = controlledMoveFallbackPolicy;
		_timeControl      = timeControl;
		_timeControl?.Validate();
		_positionAnalysis = new(
			_moveListClient,
			ValidatePositive(moveListAnalysisTimeMs, nameof(moveListAnalysisTimeMs)),
			ValidatePositive(moveListFallbackTimeMs, nameof(moveListFallbackTimeMs))
		);
	}

	/// <summary>
	///     Creates a human-versus-engine playable match session using the supplied player color as the analysis
	///     perspective.
	/// </summary>
	public UciPlayableMatchSession(
		UciEngineClient           playingClient,
		UciEngineClient           analysisClient,
		UciEngineClient           moveListClient,
		char                      playerColor,
		int                       engineMoveTimeMs       = 1_000,
		int                       moveListAnalysisTimeMs = 3_000,
		int                       moveListFallbackTimeMs = 250,
		PlayableMatchClaimableDrawPolicy claimableDrawPolicy = PlayableMatchClaimableDrawPolicy.Automatic,
		PlayableMatchDrawOfferPolicy drawOfferPolicy = PlayableMatchDrawOfferPolicy.ExpireOnMove,
		PlayableMatchControlledMoveFallbackPolicy controlledMoveFallbackPolicy = PlayableMatchControlledMoveFallbackPolicy.UseLocalFallback,
		PlayableMatchTimeControl? timeControl            = null,
		Func<DateTimeOffset>?     utcNowProvider         = null)
		: this(
			playingClient,
			analysisClient,
			moveListClient,
			perspectiveColor: playerColor,
			whiteController: playerColor == 'w' ? MatchSideControllerKind.Manual : MatchSideControllerKind.Engine,
			blackController: playerColor == 'b' ? MatchSideControllerKind.Manual : MatchSideControllerKind.Engine,
			engineMoveTimeMs,
			moveListAnalysisTimeMs,
			moveListFallbackTimeMs,
			claimableDrawPolicy,
			drawOfferPolicy,
			controlledMoveFallbackPolicy,
			timeControl,
			utcNowProvider
		) { }

	/// <summary>
	///     Gets the side used for player-relative advantage and board-orientation helpers.
	/// </summary>
	public char PerspectiveColor { get; }

	/// <summary>
	///     Gets the controller kind for White.
	/// </summary>
	public MatchSideControllerKind WhiteController { get; }

	/// <summary>
	///     Gets the controller kind for Black.
	/// </summary>
	public MatchSideControllerKind BlackController { get; }

	/// <summary>
	///     Gets the single manual side for compatibility with human-versus-engine workflows.
	///     This member is only valid when exactly one side is manual.
	/// </summary>
	public char PlayerColor => ResolveSingleControllerColor(MatchSideControllerKind.Manual, nameof(PlayerColor));

	/// <summary>
	///     Gets the single engine-controlled side for compatibility with human-versus-engine workflows.
	///     This member is only valid when exactly one side is engine-controlled.
	/// </summary>
	public char EngineColor => ResolveSingleControllerColor(MatchSideControllerKind.Engine, nameof(EngineColor));

	/// <summary>
	///     Gets the current played-move history.
	/// </summary>
	public ImmutableArray<PlayedMove> MoveHistory => [.. _moveHistory];

	/// <summary>
	///     Gets the current raw played move list.
	/// </summary>
	public ImmutableArray<string> PlayedMoves => [.. _playedMoves];

	/// <summary>
	///     Gets the current refreshed match state.
	/// </summary>
	/// <exception cref="InvalidOperationException">Thrown when the position has not been refreshed yet.</exception>
	public PlayableMatchState CurrentState =>
		_hasCurrentState
			? _currentState
			: throw new InvalidOperationException(
				"The current position is not available until RefreshAsync has completed."
			);

	/// <summary>
	///     Gets the current pending promotion request, when applicable.
	/// </summary>
	public PendingPromotionRequest? PendingPromotion => _pendingPromotion;

	/// <summary>
	///     Tries to resolve a cached legal-move classification for the current position.
	/// </summary>
	public bool TryGetLegalMoveClassification(string move, out MoveClassification classification)
	{
		string normalizedMove = NormalizeCompletedMove(move);
		return GetCurrentLegalMoveClassifications().TryGetValue(normalizedMove, out classification);
	}

	/// <summary>
	///     Tries to resolve a played move's latest move classification.
	/// </summary>
	public bool TryGetPlayedMoveClassification(PlayedMove move, out MoveClassification classification)
	{
		var classifications = _classifications.GetKnown(move.ParentPositionKey);
		if (classifications.TryGetValue(move.Move, out classification))
			return true;

		classification = move.Classification;
		return classification.MovingPiece != '\0';
	}

	/// <summary>
	///     Tries to resolve a played move's high-quality score from the cached parent-position analysis.
	/// </summary>
	public bool TryGetPlayedMoveScore(PlayedMove move, out PositionScore score) =>
		move.TryResolveScore(ResolvePositionAnalysis, out score);

	/// <summary>
	///     Tries to resolve a cached full-strength analysis for the supplied position key.
	/// </summary>
	public bool TryGetPositionAnalysis(string positionKey, out PositionAnalysisResult analysis) =>
		_positionAnalysis.TryGetAnalysis(positionKey, out analysis);

	/// <summary>
	///     Gets the latest cached legal-move classifications for the current position.
	/// </summary>
	public ImmutableDictionary<string, MoveClassification> GetCurrentLegalMoveClassifications() =>
		_classifications.GetKnown(CurrentState.PositionKey);

	/// <summary>
	///     Resolves the current position advantage from the best completed cached analysis available so far.
	/// </summary>
	public PositionAdvantage ResolveCurrentAdvantage()
	{
		var state = CurrentState;
		return _moveHistory.ResolveCurrentAdvantage(
			state.PositionKey,
			state.LegalMoves.Length,
			ResolvePositionAnalysis
		);
	}

	/// <summary>
	///     Builds simple debugging lines for the played-move history using the best completed cached scores available so far.
	/// </summary>
	public string[] GetMoveHistoryDisplayLines() =>
		_moveHistory.ToDisplayLines(ResolvePlayedMoveScore);

	/// <summary>
	///     Returns whether the requested number of played moves can currently be undone.
	/// </summary>
	/// <param name="count">Number of moves to undo.</param>
	/// <returns><see langword="true" /> when at least <paramref name="count" /> moves have been played.</returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="count" /> is not greater than zero.</exception>
	public bool CanUndoMoves(int count = 1)
	{
		ValidatePositive(count, nameof(count));
		return _playedMoves.Count >= count;
	}

	/// <summary>
	///     Undoes the requested number of played moves, preserving cached analysis and classifications for the retained
	///     move prefix while canceling obsolete background work.
	/// </summary>
	/// <param name="count">Number of moves to undo.</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="count" /> is not greater than zero.</exception>
	/// <exception cref="InvalidOperationException">Thrown when fewer than <paramref name="count" /> moves have been played.</exception>
	public void UndoMoves(int count = 1)
	{
		ValidatePositive(count, nameof(count));
		if (!CanUndoMoves(count))
			throw new InvalidOperationException("Cannot undo more moves than have been played.");

		int remainingMoveCount = _playedMoves.Count - count;
		if (_playedMoves.Count > remainingMoveCount)
			_playedMoves.RemoveRange(remainingMoveCount, _playedMoves.Count - remainingMoveCount);

		int retainedHistoryCount = Math.Min(_moveHistory.Count, remainingMoveCount);
		if (_moveHistory.Count > retainedHistoryCount)
			_moveHistory.RemoveRange(retainedHistoryCount, _moveHistory.Count - retainedHistoryCount);

		if (_clockHistory.Count > 0 && _clockHistory.Count > remainingMoveCount + 1)
			_clockHistory.RemoveRange(remainingMoveCount + 1, _clockHistory.Count - (remainingMoveCount + 1));

		_pendingPromotion = null;
		_forcedResult = default;
		_claimableResult = null;
		_drawOfferedBy = null;
		_isClockPaused = false;

		var retainedPositionKeys = CollectRetainedPositionKeys();
		_positionAnalysis.CancelPendingAndRetainCompleted(retainedPositionKeys);
		_classifications.CancelPendingAndRetain(retainedPositionKeys);

		_hasCurrentState = false;
		_currentState    = default;
		RaiseEvent(PlayableMatchEventKind.MovesUndone, undoCount: count);
	}

	/// <summary>
	///     Loads an arbitrary base position and optional played-move sequence into the session.
	/// </summary>
	public async Task LoadPositionAsync(
		Fen                  baseFen,
		IEnumerable<string>? moves = null,
		CancellationToken    ct    = default)
	{
		_baseFen = baseFen;
		_playedMoves.Clear();
		_moveHistory.Clear();
		_pendingPromotion = null;
		_lastResult = default;
		_forcedResult = default;
		_claimableResult = null;
		_drawOfferedBy = null;
		_isClockPaused = false;

		if (moves is { })
			foreach (string move in moves)
				_playedMoves.Add(NormalizeCompletedMove(move));

		InitializeClocks(baseFen.ActiveColor);
		ResetBackgroundState();
		_hasCurrentState = false;
		_currentState    = default;

		await Task.WhenAll(
			_playingClient.UciNewGameAsync(ct),
			_analysisClient.UciNewGameAsync(ct),
			_moveListClient.UciNewGameAsync(ct)
		).ConfigureAwait(false);

		RaiseEvent(PlayableMatchEventKind.PositionLoaded, state: _hasCurrentState ? _currentState : null);
	}

	/// <summary>
	///     Starts a new game from the standard initial position.
	/// </summary>
	public async Task StartNewGameAsync(CancellationToken ct = default)
	{
		await LoadPositionAsync(Fen.Default, [], ct).ConfigureAwait(false);
		RaiseEvent(PlayableMatchEventKind.GameStarted);
	}

	/// <summary>
	///     Plays the engine's next move using the configured engine move time.
	///     This compatibility alias is only valid when the current side is engine-controlled.
	/// </summary>
	public Task<EngineMoveResult> PlayEngineMoveAsync(CancellationToken ct = default) => PlayControlledMoveAsync(ct);

	/// <summary>
	///     Plays the current side's move when that side is engine-controlled.
	/// </summary>
	public async Task<EngineMoveResult> PlayControlledMoveAsync(CancellationToken ct = default)
	{
		EnsureTurnHasTimeRemaining(_timeControl.HasValue ? _utcNowProvider() : null);

		var state = CurrentState;
		if (GetController(state.Fen.ActiveColor) != MatchSideControllerKind.Engine)
			throw new InvalidOperationException("The current side is not engine-controlled.");

		SearchResult result = default;
		string move;
		try
		{
			result = await _playingClient.GoAsync(new() { MoveTimeMs = _engineMoveTimeMs }, ct).ConfigureAwait(false);
			move = result.BestMove.ToLowerInvariant();
		}
		catch (InvalidOperationException)
		{
			if (_controlledMoveFallbackPolicy == PlayableMatchControlledMoveFallbackPolicy.Throw)
				throw;

			move = SelectFallbackControlledMove(state);
		}

		if (!state.LegalMoves.ContainsUciMove(move))
			throw new InvalidOperationException(
				$"Engine produced '{move}', which is not legal in the current position."
			);

		CompleteMove(move, state.Fen.ActiveColor);
		return new(move, result);
	}

	/// <summary>
	///     Waits for tactical move classifications to complete for the current position.
	/// </summary>
	public Task<ImmutableDictionary<string, MoveClassification>> WaitForCurrentMoveClassificationsAsync(
		CancellationToken ct = default)
	{
		string positionKey = CurrentState.PositionKey;
		return _classifications.WaitAsync(positionKey, ct);
	}

	/// <summary>
	///     Refreshes the current board snapshot, legal moves, live classifications, and non-blocking player-relative
	///     advantage.
	/// </summary>
	public async Task<PlayableMatchState> RefreshAsync(CancellationToken ct = default)
	{
		Fen currentFen = BuildCurrentFen();

		await Task.WhenAll(
			_playingClient.SetPositionAsync(_baseFen, _playedMoves, ct),
			_analysisClient.SetPositionAsync(_baseFen, _playedMoves, ct)
		).ConfigureAwait(false);

		var legalMoves = currentFen.GetLegalMoves();
		string positionKey = currentFen.Raw;

		UpdateMoveHistory(positionKey);
		_classifications.Enqueue(positionKey, currentFen, legalMoves);
		EnrichMoveHistoryClassifications();

		if (legalMoves.Length > 0)
			_positionAnalysis.Enqueue(positionKey, _playedMoves, currentFen.ActiveColor, PerspectiveColor, legalMoves);

		var advantage = _moveHistory.ResolveCurrentAdvantage(
			positionKey,
			legalMoves.Length,
			ResolvePositionAnalysis
		);

		var classifications = _classifications.GetKnown(positionKey);
		var clock = GetClockSnapshot();
		var outcome = EvaluateOutcome(currentFen, legalMoves, clock);
		_claimableResult = outcome.ClaimableResult;
		var currentResult = outcome.Result;
		_currentState = new(
			currentFen,
			positionKey,
			legalMoves,
			classifications,
			advantage,
			[.. _moveHistory],
			_pendingPromotion,
			currentResult,
			outcome.ClaimableResult,
			_drawOfferedBy,
			clock
		);
		_hasCurrentState = true;
		RaiseEvent(PlayableMatchEventKind.PositionRefreshed, state: _currentState);
		if (_lastResult != currentResult)
		{
			_lastResult = currentResult;
			if (currentResult.IsTerminal)
				RaiseEvent(PlayableMatchEventKind.ResultChanged, state: _currentState, result: currentResult);
		}

		return _currentState;
	}

	/// <summary>
	///     Returns the full-strength legal-move analysis for the current position, enriched with any known move
	///     classifications.
	/// </summary>
	public async Task<PositionAnalysisResult> GetLegalMoveAnalysisAsync(CancellationToken ct = default)
	{
		var analysis = await _positionAnalysis.GetAnalysisAsync(CurrentState.PositionKey).ConfigureAwait(false);
		var classifications = GetCurrentLegalMoveClassifications();
		if (analysis.Evaluations.IsDefaultOrEmpty || classifications.Count == 0)
			return analysis;

		var evaluations = analysis.Evaluations
			.Select(evaluation =>
				classifications.TryGetValue(evaluation.Move, out var classification)
					? evaluation with { Classification = classification }
					: evaluation)
			.ToImmutableArray();

		return new(analysis.Advantage, evaluations);
	}

	/// <summary>
	///     Applies a validated human move to the current match state.
	///     This compatibility alias is only valid when the current side is manually controlled.
	/// </summary>
	public void ApplyHumanMove(string move) => ApplyMove(move);

	/// <summary>
	///     Applies a validated move for the current side when that side is externally controlled.
	/// </summary>
	public void ApplyMove(string move)
	{
		if (_pendingPromotion.HasValue)
			throw new InvalidOperationException("Choose a promotion piece before applying another move.");

		DateTimeOffset? moveTimestamp = _timeControl.HasValue ? _utcNowProvider() : null;
		EnsureTurnHasTimeRemaining(moveTimestamp);

		string normalizedMove = NormalizeMoveOrPrefix(move);
		var state = CurrentState;

		if (GetController(state.Fen.ActiveColor) == MatchSideControllerKind.Engine)
			throw new InvalidOperationException("A manual move cannot be applied on an engine-controlled turn.");

		if (state.LegalMoves.ContainsUciMove(normalizedMove))
		{
			CompleteMove(normalizedMove, state.Fen.ActiveColor, moveTimestamp);
			return;
		}

		if (LocalFenRules.TryCreatePendingPromotion(state.Fen, normalizedMove, state.LegalMoves, out var request))
		{
			_pendingPromotion = request;
			RaiseEvent(PlayableMatchEventKind.PromotionRequired, pendingPromotion: request);
			return;
		}

		RaiseEvent(
			PlayableMatchEventKind.IllegalMoveRejected,
			state: state,
			move: normalizedMove,
			error: "The move is not legal in the current position."
		);
		throw new InvalidOperationException("The move is not legal in the current position.");
	}

	/// <summary>
	///     Completes the current pending promotion using the supplied lowercase promotion suffix.
	/// </summary>
	/// <param name="promotionPiece">Promotion suffix such as <c>q</c>, <c>r</c>, <c>b</c>, or <c>n</c>.</param>
	public void ChoosePromotion(char promotionPiece)
	{
		if (!_pendingPromotion.HasValue)
			throw new InvalidOperationException("There is no pending promotion to complete.");

		char normalizedPiece = char.ToLowerInvariant(promotionPiece);
		var request = _pendingPromotion.Value;
		if (!request.AllowedPromotionPieces.Contains(normalizedPiece))
			throw new InvalidOperationException("The requested promotion piece is not legal in the current position.");

		_pendingPromotion = null;
		string move = request.MovePrefix + normalizedPiece;
		RaiseEvent(PlayableMatchEventKind.PromotionChosen, move: move);
		CompleteMove(move, request.MovingSide);
	}

	/// <summary>
	///     Offers a draw from the current side.
	/// </summary>
	public void OfferDraw()
	{
		var state = CurrentState;
		if (state.Result.IsTerminal)
			throw new InvalidOperationException("The match has already ended.");

		_drawOfferedBy = state.Fen.ActiveColor;
		UpdateCurrentStateMetadata();
		RaiseEvent(PlayableMatchEventKind.DrawOffered, state: _hasCurrentState ? _currentState : null);
	}

	/// <summary>
	///     Accepts a pending draw offer from the opposing side.
	/// </summary>
	public void AcceptDraw()
	{
		if (!_drawOfferedBy.HasValue)
			throw new InvalidOperationException("There is no opposing draw offer to accept.");

		_drawOfferedBy = null;
		SetForcedResult(new(PlayableMatchResultReason.DrawAgreement, null));
	}

	/// <summary>
	///     Declines a pending draw offer from the opposing side.
	/// </summary>
	public void DeclineDraw()
	{
		if (!_drawOfferedBy.HasValue)
			throw new InvalidOperationException("There is no opposing draw offer to decline.");

		_drawOfferedBy = null;
		UpdateCurrentStateMetadata();
		RaiseEvent(PlayableMatchEventKind.DrawDeclined, state: _hasCurrentState ? _currentState : null);
	}

	/// <summary>
	///     Claims the current claimable draw result when explicit draw claims are enabled.
	/// </summary>
	public void ClaimDraw()
	{
		if (!_claimableResult.HasValue || !_claimableResult.Value.IsDraw)
			throw new InvalidOperationException("There is no claimable draw available.");

		SetForcedResult(_claimableResult.Value);
	}

	/// <summary>
	///     Resigns on behalf of the current side.
	/// </summary>
	public void Resign()
	{
		var state = CurrentState;
		SetForcedResult(new(PlayableMatchResultReason.Resignation, Opposite(state.Fen.ActiveColor)));
	}

	/// <summary>
	///     Pauses the active match clock.
	/// </summary>
	public void PauseClock()
	{
		if (!_timeControl.HasValue || _isClockPaused || _clockHistory.Count == 0)
			return;

		_isClockPaused = true;
		_clockHistory[^1] = _clockHistory[^1] with { PausedAtUtc = _utcNowProvider() };
		UpdateCurrentStateMetadata();
		RaiseEvent(PlayableMatchEventKind.ClockPaused, state: _hasCurrentState ? _currentState : null);
	}

	/// <summary>
	///     Resumes the active match clock.
	/// </summary>
	public void ResumeClock()
	{
		if (!_timeControl.HasValue || !_isClockPaused || _clockHistory.Count == 0)
			return;

		var checkpoint = _clockHistory[^1];
		DateTimeOffset now = _utcNowProvider();
		TimeSpan pausedDuration = checkpoint.PausedAtUtc.HasValue ? now - checkpoint.PausedAtUtc.Value : TimeSpan.Zero;
		_clockHistory[^1] = checkpoint with
		{
			PausedAccumulated = checkpoint.PausedAccumulated + pausedDuration,
			PausedAtUtc = null
		};
		_isClockPaused = false;
		UpdateCurrentStateMetadata();
		RaiseEvent(PlayableMatchEventKind.ClockResumed, state: _hasCurrentState ? _currentState : null);
	}

	/// <summary>
	///     Cancels any in-flight background analysis or classification.
	/// </summary>
	public void CancelAnalysis()
	{
		_positionAnalysis.Cancel();
		_classifications.Cancel();
	}

	/// <summary>
	///     Processes a serializable request DTO against the session.
	/// </summary>
	/// <param name="request">Request to process.</param>
	/// <param name="ct">Cancellation token.</param>
	public async Task ProcessAsync(PlayableMatchRequest request, CancellationToken ct = default)
	{
		if (request.SchemaVersion != 1)
			throw new InvalidOperationException($"Unsupported playable match request schema version '{request.SchemaVersion}'.");

		switch (request.Kind)
		{
			case PlayableMatchRequestKind.StartNewGame:
				await StartNewGameAsync(ct).ConfigureAwait(false);
				return;
			case PlayableMatchRequestKind.LoadPosition:
				if (!request.BaseFen.HasValue)
					throw new InvalidOperationException("A base FEN is required for a load-position request.");

				await LoadPositionAsync(request.BaseFen.Value, request.Moves, ct).ConfigureAwait(false);
				return;
			case PlayableMatchRequestKind.Refresh:
				await RefreshAsync(ct).ConfigureAwait(false);
				return;
			case PlayableMatchRequestKind.ApplyMove:
				ApplyMove(request.Move ?? throw new InvalidOperationException("Move is required for apply-move."));
				return;
			case PlayableMatchRequestKind.ChoosePromotion:
				if (!request.PromotionPiece.HasValue)
					throw new InvalidOperationException("Promotion piece is required for choose-promotion.");

				ChoosePromotion(request.PromotionPiece.Value);
				return;
			case PlayableMatchRequestKind.PlayControlledMove:
				await PlayControlledMoveAsync(ct).ConfigureAwait(false);
				return;
			case PlayableMatchRequestKind.UndoMoves:
				UndoMoves(request.UndoCount);
				return;
			case PlayableMatchRequestKind.OfferDraw:
				OfferDraw();
				return;
			case PlayableMatchRequestKind.AcceptDraw:
				AcceptDraw();
				return;
			case PlayableMatchRequestKind.DeclineDraw:
				DeclineDraw();
				return;
			case PlayableMatchRequestKind.ClaimDraw:
				ClaimDraw();
				return;
			case PlayableMatchRequestKind.Resign:
				Resign();
				return;
			case PlayableMatchRequestKind.PauseClock:
				PauseClock();
				return;
			case PlayableMatchRequestKind.ResumeClock:
				ResumeClock();
				return;
			case PlayableMatchRequestKind.CancelAnalysis:
				CancelAnalysis();
				return;
			default:
				throw new ArgumentOutOfRangeException(nameof(request), request.Kind, "Unknown playable match request.");
		}
	}

	/// <summary>
	///     Processes a batch of serializable requests and returns the ordered canonical events emitted while doing so.
	/// </summary>
	/// <param name="requests">Requests to process in order.</param>
	/// <param name="ct">Cancellation token.</param>
	public async Task<ImmutableArray<PlayableMatchEvent>> ProcessBatchAsync(
		IEnumerable<PlayableMatchRequest> requests,
		CancellationToken                 ct = default)
	{
		if (requests is null)
			throw new ArgumentNullException(nameof(requests));

		List<PlayableMatchEvent> events = [];
		void OnEvent(PlayableMatchEvent matchEvent) => events.Add(matchEvent);

		EventOccurred += OnEvent;
		try
		{
			foreach (var request in requests)
			{
				ct.ThrowIfCancellationRequested();
				await ProcessAsync(request, ct).ConfigureAwait(false);
			}
		}
		finally
		{
			EventOccurred -= OnEvent;
		}

		return [.. events];
	}

	/// <summary>
	///     Returns the configured controller kind for the supplied side.
	/// </summary>
	/// <param name="side">The side to inspect: <c>w</c> or <c>b</c>.</param>
	public MatchSideControllerKind GetController(char side) => NormalizeColor(side, nameof(side)) switch
	{
		'w' => WhiteController,
		_ => BlackController
	};

	private static char NormalizeColor(char color, string paramName)
	{
		if (color is 'w' or 'b')
			return color;

		throw new ArgumentOutOfRangeException(paramName, "Color must be 'w' or 'b'.");
	}

	private static int ValidatePositive(int value, string paramName)
	{
		if (value > 0)
			return value;

		throw new ArgumentOutOfRangeException(paramName, "Value must be greater than zero.");
	}

	private char ResolveSingleControllerColor(MatchSideControllerKind controller, string memberName)
	{
		if (WhiteController == controller && BlackController != controller)
			return 'w';

		if (BlackController == controller && WhiteController != controller)
			return 'b';

		throw new InvalidOperationException(
			$"{memberName} is only available when exactly one side uses the {controller} controller."
		);
	}

	private static string NormalizeCompletedMove(string move)
	{
		if (move is null)
			throw new ArgumentNullException(nameof(move));

		string normalizedMove = move.Trim().ToLowerInvariant();
		if (!UciEngineClient.IsUciMoveString(normalizedMove))
			throw new ArgumentException("Enter a move in UCI notation such as e2e4 or a7a8q.", nameof(move));

		return normalizedMove;
	}

	private static string NormalizeMoveOrPrefix(string move)
	{
		if (move is null)
			throw new ArgumentNullException(nameof(move));

		string normalizedMove = move.Trim().ToLowerInvariant();
		if (normalizedMove.Length is not 4 and not 5 ||
			!UciEngineClient.IsUciMoveString(
				normalizedMove.Length == 4 ? normalizedMove + "q" : normalizedMove
			))
		{
			throw new ArgumentException("Enter a move in UCI notation such as e2e4 or a7a8q.", nameof(move));
		}

		return normalizedMove;
	}

	private void EnrichMoveHistoryClassifications()
	{
		for (var i = 0; i < _moveHistory.Count; i++)
		{
			var move = _moveHistory[i];
			var classifications = _classifications.GetKnown(move.ParentPositionKey);
			if (!classifications.TryGetValue(move.Move, out var classification))
				continue;

			if (move.Classification == classification)
				continue;

			_moveHistory[i] = move with { Classification = classification };
		}
	}

	private void ResetBackgroundState()
	{
		_positionAnalysis.Cancel();
		_classifications.Cancel();
	}

	private HashSet<string> CollectRetainedPositionKeys()
	{
		var retainedPositionKeys = new HashSet<string>(StringComparer.Ordinal) { _baseFen.Raw };
		foreach (var move in _moveHistory)
		{
			retainedPositionKeys.Add(move.ParentPositionKey);
			retainedPositionKeys.Add(move.PositionKey);
		}

		return retainedPositionKeys;
	}

	private void UpdateMoveHistory(string currentPositionKey)
	{
		if (_moveHistory.Count == _playedMoves.Count)
			return;

		if (_moveHistory.Count != _playedMoves.Count - 1)
			throw new InvalidOperationException("Move history can only be extended by one move per position snapshot.");

		int moveIndex = _moveHistory.Count;
		string parentPositionKey = moveIndex == 0 ? _baseFen.Raw : _moveHistory[^1].PositionKey;
		var classification = _classifications.GetKnown(parentPositionKey)
			.TryGetValue(_playedMoves[moveIndex], out var knownClassification)
			? knownClassification
			: MoveClassification.Unknown();

		_moveHistory.Add(
			new(
				moveIndex / 2 + 1,
				moveIndex % 2 == 0 ? 'w' : 'b',
				_playedMoves[moveIndex],
				parentPositionKey,
				currentPositionKey,
				classification
			)
		);
	}

	private Fen BuildCurrentFen()
	{
		var current = _baseFen;
		foreach (string move in _playedMoves)
			current = current.ApplyMove(move);

		return current;
	}

	private void CompleteMove(string move, char movingSide, DateTimeOffset? completedAtUtc = null)
	{
		var previousFen = CurrentState.Fen;
		var classification = previousFen.ClassifyMoveFully(move);
		var resultingFen = previousFen.ApplyMove(move);
		var moveData = BuildMoveData(previousFen, resultingFen, move, classification);

		AdvanceClockForCompletedMove(movingSide, completedAtUtc);
		_playedMoves.Add(move);
		if (_drawOfferPolicy == PlayableMatchDrawOfferPolicy.ExpireOnMove)
			_drawOfferedBy = null;
		_forcedResult = default;
		var immediateOutcome = EvaluateOutcome(resultingFen, resultingFen.GetLegalMoves(), GetLatestClockSnapshotForFen(resultingFen));
		_claimableResult = immediateOutcome.ClaimableResult;
		_lastResult = immediateOutcome.Result;
		_hasCurrentState = false;
		_currentState = default;
		RaiseEvent(PlayableMatchEventKind.MoveApplied, move: move, moveData: moveData);
		if (immediateOutcome.Result.IsTerminal)
			RaiseEvent(PlayableMatchEventKind.ResultChanged, move: move, moveData: moveData, result: immediateOutcome.Result);
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
				_utcNowProvider(),
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
		DateTimeOffset now = completedAtUtc ?? _utcNowProvider();
		TimeSpan elapsed = ComputeElapsed(checkpoint, now);
		TimeSpan whiteRemaining = checkpoint.WhiteRemaining;
		TimeSpan blackRemaining = checkpoint.BlackRemaining;
		int whiteMoves = checkpoint.WhiteMovesCompleted;
		int blackMoves = checkpoint.BlackMovesCompleted;

		var stage = GetStageForSide(checkpoint, movingSide);
		TimeSpan delay = stage.DelayPerMove;
		TimeSpan mainElapsed = elapsed > delay ? elapsed - delay : TimeSpan.Zero;

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
		DateTimeOffset now = snapshotUtc ?? _utcNowProvider();
		TimeSpan elapsed = ComputeElapsed(checkpoint, now);
		TimeSpan whiteRemaining = checkpoint.WhiteRemaining;
		TimeSpan blackRemaining = checkpoint.BlackRemaining;
		var stage = GetStageForSide(checkpoint, checkpoint.ActiveColor);
		TimeSpan delayRemaining = elapsed < stage.DelayPerMove ? stage.DelayPerMove - elapsed : TimeSpan.Zero;
		TimeSpan mainElapsed = elapsed > stage.DelayPerMove ? elapsed - stage.DelayPerMove : TimeSpan.Zero;

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

		TimeSpan currentRemaining = clock.Value.ActiveColor == 'w'
			? clock.Value.WhiteRemaining
			: clock.Value.BlackRemaining;
		if (currentRemaining > TimeSpan.Zero)
			return;

		throw new InvalidOperationException("The current side has already lost on time.");
	}

	private MatchOutcome EvaluateOutcome(
		Fen                    fen,
		ImmutableArray<string> legalMoves,
		PlayableMatchClockState? clock)
	{
		if (_forcedResult.IsTerminal)
			return new(_forcedResult, null);

		if (clock.HasValue)
		{
			TimeSpan activeRemaining = fen.ActiveColor == 'w'
				? clock.Value.WhiteRemaining
				: clock.Value.BlackRemaining;
			if (_timeControl.HasValue &&
				_timeControl.Value.TimeoutPolicy == PlayableMatchTimeoutPolicy.AutomaticLoss &&
				activeRemaining <= TimeSpan.Zero)
			{
				return new(new(PlayableMatchResultReason.Timeout, Opposite(fen.ActiveColor)), null);
			}
		}

		if (legalMoves.IsDefaultOrEmpty || legalMoves.Length == 0)
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
		int count = LocalFenRules.BuildRepetitionKey(_baseFen) == currentKey ? 1 : 0;
		foreach (var move in _moveHistory)
		{
			var fen = Fen.Parse(move.PositionKey);
			if (fen.HasValue && LocalFenRules.BuildRepetitionKey(fen.Value) == currentKey)
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

	private PositionAnalysisResult? ResolvePositionAnalysis(string positionKey) =>
		TryGetPositionAnalysis(positionKey, out var analysis) ? analysis : null;

	private PositionScore? ResolvePlayedMoveScore(PlayedMove move) =>
		TryGetPlayedMoveScore(move, out var score) ? score : null;

	private void RaiseEvent(
		PlayableMatchEventKind   kind,
		PlayableMatchState?      state            = null,
		string?                  move             = null,
		PlayableMatchMoveData?   moveData         = null,
		PlayableMatchResult?     result           = null,
		PendingPromotionRequest? pendingPromotion = null,
		int?                     undoCount        = null,
		string?                  error            = null)
	{
		try
		{
			EventOccurred?.Invoke(
				new(
					kind,
					DateTimeOffset.UtcNow,
					state,
					move,
					moveData,
					result,
					pendingPromotion,
					undoCount,
					error
				)
			);
		}
		catch
		{
			// External subscribers must not interfere with match orchestration.
		}
	}

	private static TimeSpan ClampToZero(TimeSpan remaining) =>
		remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;

	private void SetForcedResult(PlayableMatchResult result)
	{
		_forcedResult = result;
		_claimableResult = null;
		_drawOfferedBy = null;
		_lastResult = result;
		UpdateCurrentStateMetadata(resultOverride: result);
		RaiseEvent(PlayableMatchEventKind.ResultChanged, state: _hasCurrentState ? _currentState : null, result: result);
	}

	private void UpdateCurrentStateMetadata(PlayableMatchResult? resultOverride = null)
	{
		if (!_hasCurrentState)
			return;

		_currentState = _currentState with
		{
			PendingPromotion = _pendingPromotion,
			Result = resultOverride ?? _currentState.Result,
			ClaimableResult = _claimableResult,
			DrawOfferedBy = _drawOfferedBy,
			Clock = GetClockSnapshot()
		};
	}

	private PlayableMatchMoveData BuildMoveData(
		Fen                previousFen,
		Fen                resultingFen,
		string             move,
		MoveClassification classification)
	{
		char movingSymbol = classification.MovingPiece;
		var movingPiece = BuildPiece(movingSymbol);
		PlayableMatchPiece? capturedPiece = classification.CapturedPiece.HasValue
			? BuildPiece(classification.CapturedPiece.Value)
			: null;
		PlayableMatchPiece? promotionPiece = classification.IsPromotion
			? BuildPiece(previousFen.ActiveColor == 'w' ? char.ToUpperInvariant(move[4]) : move[4])
			: null;
		PlayableMatchSecondaryMove? secondaryMove = BuildSecondaryMove(previousFen.ActiveColor, classification);

		return new(
			_playedMoves.Count,
			(_playedMoves.Count / 2) + 1,
			previousFen.ActiveColor,
			move,
			move[..2],
			move.Substring(2, 2),
			movingPiece,
			capturedPiece,
			promotionPiece,
			secondaryMove,
			classification,
			previousFen,
			resultingFen
		);
	}

	private static PlayableMatchPiece BuildPiece(char symbol) =>
		new(char.IsUpper(symbol) ? 'w' : 'b', char.ToLowerInvariant(symbol), symbol);

	private static PlayableMatchSecondaryMove? BuildSecondaryMove(char movingSide, MoveClassification classification)
	{
		if (classification.IsKingsideCastling)
		{
			char rook = movingSide == 'w' ? 'R' : 'r';
			return new(
				movingSide == 'w' ? "h1" : "h8",
				movingSide == 'w' ? "f1" : "f8",
				BuildPiece(rook)
			);
		}

		if (classification.IsQueensideCastling)
		{
			char rook = movingSide == 'w' ? 'R' : 'r';
			return new(
				movingSide == 'w' ? "a1" : "a8",
				movingSide == 'w' ? "d1" : "d8",
				BuildPiece(rook)
			);
		}

		return null;
	}

	private PlayableMatchClockState? GetLatestClockSnapshotForFen(Fen fen)
	{
		if (!_timeControl.HasValue || _clockHistory.Count == 0)
			return null;

		var checkpoint = _clockHistory[^1];
		return new(
			checkpoint.WhiteRemaining,
			checkpoint.BlackRemaining,
			fen.ActiveColor,
			GetStageForSide(checkpoint, checkpoint.ActiveColor).DelayPerMove,
			_isClockPaused,
			checkpoint.ActiveStageIndex,
			checkpoint.TurnStartedAtUtc
		);
	}

	private TimeSpan ComputeElapsed(ClockCheckpoint checkpoint, DateTimeOffset now)
	{
		DateTimeOffset effectiveNow = checkpoint.PausedAtUtc ?? now;
		TimeSpan elapsed = effectiveNow - checkpoint.TurnStartedAtUtc - checkpoint.PausedAccumulated;
		return elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed;
	}

	private StageSettings GetStageForSide(ClockCheckpoint checkpoint, char side)
	{
		int movesCompleted = side == 'w' ? checkpoint.WhiteMovesCompleted : checkpoint.BlackMovesCompleted;
		int stageIndex = GetStageIndexForSide(movesCompleted);
		if (!_timeControl.HasValue || stageIndex == 0)
			return new(_timeControl?.IncrementPerMove ?? TimeSpan.Zero, _timeControl?.DelayPerMove ?? TimeSpan.Zero);

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

	private static string SelectFallbackControlledMove(PlayableMatchState state)
	{
		if (state.LegalMoves.IsDefaultOrEmpty || state.LegalMoves.Length == 0)
			throw new InvalidOperationException("No legal moves are available for the current controlled side.");

		string? checkMove = null;
		string? captureMove = null;
		string? promotionMove = null;

		foreach (string legalMove in state.LegalMoves)
		{
			var classification = state.Fen.ClassifyMoveFully(legalMove);
			if (classification.IsMate)
				return legalMove;

			if (checkMove is null && classification.IsCheck)
				checkMove = legalMove;

			if (captureMove is null && classification.IsCapture)
				captureMove = legalMove;

			if (promotionMove is null && classification.IsPromotion)
				promotionMove = legalMove;
		}

		return checkMove ?? captureMove ?? promotionMove ?? state.LegalMoves[0];
	}

	/// <summary>
	///     Plays engine-controlled turns until the match is terminal or the supplied ply limit is reached.
	/// </summary>
	public async Task<PlayableMatchState> PlayUntilTerminalAsync(int maxPlies = int.MaxValue, CancellationToken ct = default)
	{
		if (maxPlies <= 0)
			throw new ArgumentOutOfRangeException(nameof(maxPlies), "Value must be greater than zero.");

		if (!_hasCurrentState)
			await RefreshAsync(ct).ConfigureAwait(false);

		for (var i = 0; i < maxPlies; i++)
		{
			var state = CurrentState;
			if (state.Result.IsTerminal)
				return state;

			if (GetController(state.Fen.ActiveColor) != MatchSideControllerKind.Engine)
				throw new InvalidOperationException("PlayUntilTerminalAsync requires engine control for the current side.");

			await PlayControlledMoveAsync(ct).ConfigureAwait(false);
			await RefreshAsync(ct).ConfigureAwait(false);
		}

		return CurrentState;
	}

	private static char Opposite(char color) => color == 'w' ? 'b' : 'w';

	private readonly record struct ClockCheckpoint(
		TimeSpan       WhiteRemaining,
		TimeSpan       BlackRemaining,
		char           ActiveColor,
		int            WhiteMovesCompleted,
		int            BlackMovesCompleted,
		int            ActiveStageIndex,
		DateTimeOffset TurnStartedAtUtc,
		DateTimeOffset? PausedAtUtc,
		TimeSpan       PausedAccumulated
	);

	private readonly record struct MatchOutcome(
		PlayableMatchResult  Result,
		PlayableMatchResult? ClaimableResult
	);

	private readonly record struct StageSettings(
		TimeSpan IncrementPerMove,
		TimeSpan DelayPerMove
	);
}
