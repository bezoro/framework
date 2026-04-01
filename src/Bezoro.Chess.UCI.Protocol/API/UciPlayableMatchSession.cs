using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.Chess.UCI.Protocol.API.Common.Extensions;
using Bezoro.Chess.UCI.Protocol.Internal;

namespace Bezoro.Chess.UCI.Protocol.API;

/// <summary>
///     Coordinates a playable match using separate playing, snapshot, and full-strength analysis clients, plus local
///     background move classification.
/// </summary>
public sealed class UciPlayableMatchSession
{
	private readonly MoveClassificationCoordinator  _classifications = new();
	private readonly int                            _engineMoveTimeMs;
	private readonly List<PlayedMove>               _moveHistory = [];
	private readonly List<string>                   _playedMoves = [];
	private readonly UciEngineClient                _analysisClient;
	private readonly UciEngineClient                _moveListClient;
	private readonly UciEngineClient                _playingClient;
	private readonly UciPositionAnalysisCoordinator _positionAnalysis;
	private          bool                           _hasCurrentState;
	private          Fen                            _baseFen = Fen.Default;
	private          PlayableMatchState             _currentState;

	/// <summary>
	///     Creates a playable match session.
	/// </summary>
	public UciPlayableMatchSession(
		UciEngineClient         playingClient,
		UciEngineClient         analysisClient,
		UciEngineClient         moveListClient,
		char                    perspectiveColor,
		MatchSideControllerKind whiteController,
		MatchSideControllerKind blackController,
		int                     engineMoveTimeMs       = 1_000,
		int                     moveListAnalysisTimeMs = 3_000,
		int                     moveListFallbackTimeMs = 250)
	{
		_playingClient    = playingClient ?? throw new ArgumentNullException(nameof(playingClient));
		_analysisClient   = analysisClient ?? throw new ArgumentNullException(nameof(analysisClient));
		_moveListClient   = moveListClient ?? throw new ArgumentNullException(nameof(moveListClient));
		_engineMoveTimeMs = ValidatePositive(engineMoveTimeMs, nameof(engineMoveTimeMs));
		PerspectiveColor  = NormalizeColor(perspectiveColor, nameof(perspectiveColor));
		WhiteController   = whiteController;
		BlackController   = blackController;
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
		UciEngineClient playingClient,
		UciEngineClient analysisClient,
		UciEngineClient moveListClient,
		char            playerColor,
		int             engineMoveTimeMs       = 1_000,
		int             moveListAnalysisTimeMs = 3_000,
		int             moveListFallbackTimeMs = 250)
		: this(
			playingClient,
			analysisClient,
			moveListClient,
			perspectiveColor: playerColor,
			whiteController: playerColor == 'w' ? MatchSideControllerKind.Manual : MatchSideControllerKind.Engine,
			blackController: playerColor == 'b' ? MatchSideControllerKind.Manual : MatchSideControllerKind.Engine,
			engineMoveTimeMs,
			moveListAnalysisTimeMs,
			moveListFallbackTimeMs
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
	///     Tries to resolve a cached legal-move classification for the current position.
	/// </summary>
	public bool TryGetLegalMoveClassification(string move, out MoveClassification classification)
	{
		string normalizedMove = NormalizeMove(move);
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

		var retainedPositionKeys = CollectRetainedPositionKeys();
		_positionAnalysis.CancelPendingAndRetainCompleted(retainedPositionKeys);
		_classifications.CancelPendingAndRetain(retainedPositionKeys);

		_hasCurrentState = false;
		_currentState    = default;
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

		if (moves is { })
			foreach (string move in moves)
				_playedMoves.Add(NormalizeMove(move));

		ResetBackgroundState();
		_hasCurrentState = false;
		_currentState    = default;

		var tasks = new List<Task>
		{
			_playingClient.UciNewGameAsync(ct),
			_analysisClient.UciNewGameAsync(ct),
			_moveListClient.UciNewGameAsync(ct)
		};

		await Task.WhenAll(tasks).ConfigureAwait(false);
	}

	/// <summary>
	///     Starts a new game from the standard initial position.
	/// </summary>
	public Task StartNewGameAsync(CancellationToken ct = default) => LoadPositionAsync(Fen.Default, [], ct);

	/// <summary>
	///     Plays the engine's next move using the configured engine move time.
	///     This compatibility alias is only valid when the current side is engine-controlled.
	/// </summary>
	public async Task<EngineMoveResult> PlayEngineMoveAsync(CancellationToken ct = default)
		=> await PlayControlledMoveAsync(ct).ConfigureAwait(false);

	/// <summary>
	///     Plays the current side's move when that side is engine-controlled.
	/// </summary>
	public async Task<EngineMoveResult> PlayControlledMoveAsync(CancellationToken ct = default)
	{
		var state = CurrentState;
		if (GetController(state.Fen.ActiveColor) != MatchSideControllerKind.Engine)
			throw new InvalidOperationException("The current side is not engine-controlled.");

		var result = await _playingClient.GoAsync(new() { MoveTimeMs = _engineMoveTimeMs }, ct).ConfigureAwait(false);
		string move = result.BestMove.ToLowerInvariant();

		if (!state.LegalMoves.ContainsUciMove(move))
			throw new InvalidOperationException(
				$"Engine produced '{result.BestMove}', which is not legal in the current position."
			);

		_playedMoves.Add(move);
		_hasCurrentState = false;
		return new(move, result);
	}

	/// <summary>
	///     Waits for tactical move classifications to complete for the current position.
	/// </summary>
	public async Task<ImmutableDictionary<string, MoveClassification>> WaitForCurrentMoveClassificationsAsync(
		CancellationToken ct = default)
	{
		string                                                positionKey = CurrentState.PositionKey;
		return await _classifications.WaitAsync(positionKey, ct).ConfigureAwait(false);
	}

	/// <summary>
	///     Refreshes the current board snapshot, legal moves, live classifications, and non-blocking player-relative
	///     advantage.
	/// </summary>
	public async Task<PlayableMatchState> RefreshAsync(CancellationToken ct = default)
	{
		await Task.WhenAll(
			_playingClient.SetPositionAsync(_baseFen, _playedMoves, ct),
			_analysisClient.SetPositionAsync(_baseFen, _playedMoves, ct)
		).ConfigureAwait(false);

		var fen = await _analysisClient.TryGetFenViaDisplayBoardAsync(ct).ConfigureAwait(false);
		if (!fen.HasValue)
			throw new NotSupportedException(
				"This playable-match workflow requires an engine that supports the non-standard 'd' command and returns a FEN line."
			);

		var legalMoves =
			(await _analysisClient.GetLegalMovesViaPerftAsync(ct).ConfigureAwait(false)).NormalizeUciMoves();

		if (_playedMoves.Count == 0 && legalMoves.Length == 0)
			throw new NotSupportedException(
				"This playable-match workflow requires an engine that supports legal-move listing via the non-standard 'go perft 1' command."
			);

		string positionKey = fen.Value.Raw;
		UpdateMoveHistory(positionKey);
		_classifications.Enqueue(positionKey, fen.Value, legalMoves);
		EnrichMoveHistoryClassifications();

		if (legalMoves.Length > 0)
			_positionAnalysis.Enqueue(positionKey, _playedMoves, fen.Value.ActiveColor, PerspectiveColor, legalMoves);

		var advantage = _moveHistory.ResolveCurrentAdvantage(
			positionKey,
			legalMoves.Length,
			ResolvePositionAnalysis
		);

		var classifications = _classifications.GetKnown(positionKey);
		_currentState    = new(fen.Value, positionKey, legalMoves, classifications, advantage, [.. _moveHistory]);
		_hasCurrentState = true;
		return _currentState;
	}

	/// <summary>
	///     Returns the full-strength legal-move analysis for the current position, enriched with any known move
	///     classifications.
	/// </summary>
	public async Task<PositionAnalysisResult> GetLegalMoveAnalysisAsync(CancellationToken ct = default)
	{
		var analysis        = await _positionAnalysis.GetAnalysisAsync(CurrentState.PositionKey).ConfigureAwait(false);
		var classifications = GetCurrentLegalMoveClassifications();
		if (analysis.Evaluations.IsDefaultOrEmpty || classifications.Count == 0)
			return analysis;

		var evaluations = analysis.Evaluations
								  .Select(evaluation =>
											  classifications.TryGetValue(evaluation.Move, out var classification)
												  ? evaluation with { Classification = classification }
												  : evaluation
								  )
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
		string normalizedMove = NormalizeMove(move);
		var    state          = CurrentState;

		if (GetController(state.Fen.ActiveColor) == MatchSideControllerKind.Engine)
			throw new InvalidOperationException("A manual move cannot be applied on an engine-controlled turn.");

		if (!state.LegalMoves.ContainsUciMove(normalizedMove))
			throw new InvalidOperationException("The move is not legal in the current position.");

		_playedMoves.Add(normalizedMove);
		_hasCurrentState = false;
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

	private static string NormalizeMove(string move)
	{
		if (move is null) throw new ArgumentNullException(nameof(move));

		string normalizedMove = move.Trim().ToLowerInvariant();
		if (!UciEngineClient.IsUciMoveString(normalizedMove))
			throw new ArgumentException("Enter a move in UCI notation such as e2e4 or a7a8q.", nameof(move));

		return normalizedMove;
	}

	private void EnrichMoveHistoryClassifications()
	{
		for (var i = 0; i < _moveHistory.Count; i++)
		{
			var move            = _moveHistory[i];
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

		int    moveIndex         = _moveHistory.Count;
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

	private PositionAnalysisResult? ResolvePositionAnalysis(string positionKey) =>
		TryGetPositionAnalysis(positionKey, out var analysis) ? analysis : null;

	private PositionScore? ResolvePlayedMoveScore(PlayedMove move) =>
		TryGetPlayedMoveScore(move, out var score) ? score : null;
}
