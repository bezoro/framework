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
    private readonly List<PlayableMatchClockCheckpoint> _clockHistory = [];
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
	private          int                            _clockHistoryOriginMoveCount;
	private          Fen                            _baseFen = Fen.Default;
	private          PlayableMatchState             _currentState;
	private          PlayableMatchResult?           _claimableResult;
	private          char?                          _drawOfferedBy;
	private          PlayableMatchResult            _forcedResult;
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
        )
    { }

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
	/// <returns>
	///     <see langword="true" /> when at least <paramref name="count" /> moves have been played and, for a clocked
	///     match, the retained position has a clock checkpoint.
	/// </returns>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="count" /> is not greater than zero.</exception>
	public bool CanUndoMoves(int count = 1)
	{
		ValidatePositive(count, nameof(count));
		if (_playedMoves.Count < count)
			return false;

		return _clockHistory.Count == 0 || _playedMoves.Count - count >= _clockHistoryOriginMoveCount;
	}

	/// <summary>
	///     Undoes the requested number of played moves, preserving cached analysis and classifications for the retained
	///     move prefix while canceling obsolete background work. Retained clock values and move counts are restored, and
	///     the retained turn restarts unpaused at the undo time.
	/// </summary>
	/// <param name="count">Number of moves to undo.</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="count" /> is not greater than zero.</exception>
	/// <exception cref="InvalidOperationException">
	///     Thrown when fewer than <paramref name="count" /> moves have been played or the retained position predates the
	///     available clock history.
	/// </exception>
	public void UndoMoves(int count = 1)
	{
		ValidatePositive(count, nameof(count));
		if (_playedMoves.Count < count)
			throw new InvalidOperationException("Cannot undo more moves than have been played.");

		int remainingMoveCount = _playedMoves.Count - count;
		if (_clockHistory.Count > 0 && remainingMoveCount < _clockHistoryOriginMoveCount)
			throw new InvalidOperationException("Cannot undo beyond the available clock history.");

		if (_playedMoves.Count > remainingMoveCount)
			_playedMoves.RemoveRange(remainingMoveCount, _playedMoves.Count - remainingMoveCount);

		int retainedHistoryCount = Math.Min(_moveHistory.Count, remainingMoveCount);
		if (_moveHistory.Count > retainedHistoryCount)
			_moveHistory.RemoveRange(retainedHistoryCount, _moveHistory.Count - retainedHistoryCount);

		if (_clockHistory.Count > 0)
		{
			int retainedCheckpointIndex = remainingMoveCount - _clockHistoryOriginMoveCount;
			if (_clockHistory.Count > retainedCheckpointIndex + 1)
				_clockHistory.RemoveRange(
					retainedCheckpointIndex + 1,
					_clockHistory.Count - (retainedCheckpointIndex + 1));

            _clockHistory[^1] = PlayableMatchClockKernel.RestartTurn(_clockHistory[^1], _utcNowProvider());
		}

		_pendingPromotion = null;
		_forcedResult = default;
		_claimableResult = null;
		_drawOfferedBy = null;

		var retainedPositionKeys = CollectRetainedPositionKeys();
		_positionAnalysis.CancelPendingAndRetainCompleted(retainedPositionKeys);
		_classifications.CancelPendingAndRetain(retainedPositionKeys);

		_hasCurrentState = false;
		_currentState    = default;
		RaiseEvent(PlayableMatchEventKind.MovesUndone, undoCount: count);
	}

	/// <summary>
	///     Loads a complete authored or saved match setup into the session and returns the refreshed state.
	/// </summary>
	/// <param name="setup">Complete match setup to load.</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>The refreshed match state after the setup is loaded.</returns>
	public async Task<PlayableMatchState> LoadMatchAsync(PlayableMatchSetup setup, CancellationToken ct = default)
	{
		setup.Validate();
		await LoadMatchCoreAsync(setup, ct).ConfigureAwait(false);
		var state = await RefreshAsync(ct).ConfigureAwait(false);
		RaiseEvent(PlayableMatchEventKind.PositionLoaded, state: state);
		return state;
	}

	/// <summary>
	///     Loads an arbitrary base position and optional played-move sequence into the session.
	/// </summary>
	[Obsolete("Use LoadMatchAsync(new PlayableMatchSetup(baseFen, moves), ct) instead.")]
	public async Task LoadPositionAsync(
		Fen                  baseFen,
		IEnumerable<string>? moves = null,
		CancellationToken    ct    = default)
	{
		await LoadMatchCoreAsync(new(baseFen, moves), ct).ConfigureAwait(false);
		RaiseEvent(PlayableMatchEventKind.PositionLoaded, state: _hasCurrentState ? _currentState : null);
	}

	private async Task LoadMatchCoreAsync(PlayableMatchSetup setup, CancellationToken ct)
	{
		var currentFen = setup.ResolveCurrentFen();
		_baseFen = setup.BaseFen;
		_playedMoves.Clear();
		_moveHistory.Clear();
		_pendingPromotion = null;
		_lastResult = default;
		_forcedResult = default;
		_claimableResult = null;
		_drawOfferedBy = null;

		foreach (string move in setup.PlayedMoves)
			_playedMoves.Add(move);

		var clockRestore = CreateClockRestore(currentFen, setup.Clock);
		if (clockRestore.HasValue)
			RestoreClock(currentFen.ActiveColor, clockRestore.Value);
		else
			InitializeClocks(currentFen.ActiveColor);
		_clockHistoryOriginMoveCount = _clockHistory.Count > 0 ? _playedMoves.Count : 0;
		ResetBackgroundState();
		_hasCurrentState = false;
		_currentState    = default;

		await Task.WhenAll(
			_playingClient.UciNewGameAsync(ct),
			_analysisClient.UciNewGameAsync(ct),
			_moveListClient.UciNewGameAsync(ct)
		).ConfigureAwait(false);
	}

	/// <summary>
	///     Starts a new game from the standard initial position.
	/// </summary>
	public async Task StartNewGameAsync(CancellationToken ct = default)
	{
		await LoadMatchCoreAsync(PlayableMatchSetup.Standard, ct).ConfigureAwait(false);
		RaiseEvent(PlayableMatchEventKind.PositionLoaded, state: _hasCurrentState ? _currentState : null);
		RaiseEvent(PlayableMatchEventKind.GameStarted);
	}

	/// <summary>
	///     Plays the engine's next move using the configured engine move time.
	///     This compatibility alias is only valid when the current side is engine-controlled.
	/// </summary>
	/// <remarks>Use <see cref="PlayControlledMoveAsync" /> instead.</remarks>
	[Obsolete("Use PlayControlledMoveAsync instead.", false)]
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
	///     Plays the current side automatically when it is engine-controlled and the match is still playable.
	/// </summary>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>
	///     The engine move result when a controlled move was played; otherwise <see langword="null" /> when the current
	///     side is manually controlled or the match is terminal.
	/// </returns>
	public async Task<EngineMoveResult?> PlayControlledMoveIfNeededAsync(CancellationToken ct = default)
	{
		var state = _hasCurrentState ? _currentState : await RefreshAsync(ct).ConfigureAwait(false);
		if (state.Result.IsTerminal ||
			state.LegalMoves.IsDefaultOrEmpty ||
			GetController(state.Fen.ActiveColor) != MatchSideControllerKind.Engine)
		{
			return null;
		}

		return await PlayControlledMoveAsync(ct).ConfigureAwait(false);
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
	/// <remarks>Use <see cref="ApplyMove" /> instead.</remarks>
	[Obsolete("Use ApplyMove instead.", false)]
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

		if (LocalPositionRules.TryCreatePendingPromotion(state.Fen, normalizedMove, state.LegalMoves, out var request))
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
        if (!_timeControl.HasValue || _clockHistory.Count == 0 || _clockHistory[^1].PausedAtUtc.HasValue)
			return;

        _clockHistory[^1] = PlayableMatchClockKernel.Pause(_clockHistory[^1], _utcNowProvider());
		UpdateCurrentStateMetadata();
		RaiseEvent(PlayableMatchEventKind.ClockPaused, state: _hasCurrentState ? _currentState : null);
	}

	/// <summary>
	///     Resumes the active match clock.
	/// </summary>
	public void ResumeClock()
	{
        if (!_timeControl.HasValue || _clockHistory.Count == 0 || !_clockHistory[^1].PausedAtUtc.HasValue)
			return;

        _clockHistory[^1] = PlayableMatchClockKernel.Resume(_clockHistory[^1], _utcNowProvider());
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

				await LoadMatchCoreAsync(new(request.BaseFen.Value, request.Moves), ct).ConfigureAwait(false);
				RaiseEvent(PlayableMatchEventKind.PositionLoaded, state: _hasCurrentState ? _currentState : null);
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

		if (_moveHistory.Count > _playedMoves.Count)
			throw new InvalidOperationException("Move history contains more entries than the played-move list.");

		var current = _baseFen;
		for (var i = 0; i < _moveHistory.Count; i++)
			current = current.ApplyMove(_playedMoves[i]);

		for (int moveIndex = _moveHistory.Count; moveIndex < _playedMoves.Count; moveIndex++)
		{
			string move = _playedMoves[moveIndex];
			string parentPositionKey = current.Raw;
			var classification = _classifications.GetKnown(parentPositionKey)
				.TryGetValue(move, out var knownClassification)
				? knownClassification
				: MoveClassification.Unknown();
			char movingSide = current.ActiveColor;
			int ply = current.FullmoveNumber;
			current = current.ApplyMove(move);

			_moveHistory.Add(
				new(
					ply,
					movingSide,
					move,
					parentPositionKey,
					current.Raw,
					classification
				)
			);
		}

		if (_moveHistory.Count > 0 && _moveHistory[^1].PositionKey != currentPositionKey)
			throw new InvalidOperationException("Move history does not match the current position snapshot.");
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
        PlayableMatchClockState? clock = _timeControl.HasValue && _clockHistory.Count > 0
            ? PlayableMatchClockKernel.SnapshotAtTurnStart(_timeControl.Value, _clockHistory[^1])
            : null;
        var immediateOutcome = EvaluateOutcome(resultingFen, resultingFen.GetLegalMoves(), clock);
		_claimableResult = immediateOutcome.ClaimableResult;
		_lastResult = immediateOutcome.Result;
		_hasCurrentState = false;
		_currentState = default;
		RaiseEvent(PlayableMatchEventKind.MoveApplied, move: move, moveData: moveData);
		if (immediateOutcome.Result.IsTerminal)
			RaiseEvent(PlayableMatchEventKind.ResultChanged, move: move, moveData: moveData, result: immediateOutcome.Result);
	}

	private PlayableMatchClockRestore? CreateClockRestore(Fen currentFen, PlayableMatchClockSetup? setup)
	{
		if (!setup.HasValue)
			return null;

		var clockSetup = setup.Value;
        DateTimeOffset now = clockSetup.SnapshotUtc ?? _utcNowProvider();
        return PlayableMatchClockKernel.CreateRestore(currentFen, clockSetup, _timeControl, now);
	}

	private void RestoreClock(char activeColor, PlayableMatchClockRestore restore)
	{
		if (!_timeControl.HasValue)
			throw new InvalidOperationException("Cannot restore clock state because this session has no time control.");

		_clockHistory.Clear();
        _clockHistory.Add(PlayableMatchClockKernel.Restore(_timeControl.Value, activeColor, restore));
	}

	private void InitializeClocks(char activeColor)
	{
		_clockHistory.Clear();
		if (!_timeControl.HasValue)
			return;

        _clockHistory.Add(PlayableMatchClockKernel.Initialize(_timeControl.Value, activeColor, _utcNowProvider()));
	}

	private void AdvanceClockForCompletedMove(char movingSide, DateTimeOffset? completedAtUtc = null)
	{
		if (!_timeControl.HasValue || _clockHistory.Count == 0)
			return;

		DateTimeOffset now = completedAtUtc ?? _utcNowProvider();
        _clockHistory.Add(PlayableMatchClockKernel.CompleteMove(_timeControl.Value, _clockHistory[^1], movingSide, now));
	}

	private PlayableMatchClockState? GetClockSnapshot(DateTimeOffset? snapshotUtc = null)
	{
		if (!_timeControl.HasValue || _clockHistory.Count == 0)
			return null;

		DateTimeOffset now = snapshotUtc ?? _utcNowProvider();
        return PlayableMatchClockKernel.Snapshot(_timeControl.Value, _clockHistory[^1], now);
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
			return LocalPositionRules.IsCurrentPlayerInCheck(fen)
				? new(new(PlayableMatchResultReason.Checkmate, Opposite(fen.ActiveColor)), null)
				: new(new(PlayableMatchResultReason.Stalemate, null), null);
		}

		if (fen.HalfmoveClock >= 100)
			return CreateClaimableOrAutomaticResult(PlayableMatchResultReason.FiftyMoveRule);

		if (LocalPositionRules.HasInsufficientMaterial(fen))
			return new(new(PlayableMatchResultReason.InsufficientMaterial, null), null);

		if (CountRepetitions(fen) >= 3)
			return CreateClaimableOrAutomaticResult(PlayableMatchResultReason.ThreefoldRepetition);

		return new(default, null);
	}

	private int CountRepetitions(Fen currentFen)
	{
		string currentKey = LocalPositionRules.BuildRepetitionKey(currentFen);
		int count = LocalPositionRules.BuildRepetitionKey(_baseFen) == currentKey ? 1 : 0;
		foreach (var move in _moveHistory)
		{
			var fen = Fen.Parse(move.PositionKey);
			if (fen.HasValue && LocalPositionRules.BuildRepetitionKey(fen.Value) == currentKey)
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

	private readonly record struct MatchOutcome(
		PlayableMatchResult  Result,
		PlayableMatchResult? ClaimableResult
	);
}
