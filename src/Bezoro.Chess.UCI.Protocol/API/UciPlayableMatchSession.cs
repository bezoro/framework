using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.Chess.UCI.Protocol.API.Common.Extensions;

namespace Bezoro.Chess.UCI.Protocol.API;

/// <summary>
///     Coordinates a playable human-versus-engine match using separate playing, snapshot, and full-strength analysis
///     clients, plus local background move classification.
/// </summary>
public sealed class UciPlayableMatchSession
{
	private readonly Dictionary<string, ImmutableDictionary<string, MoveClassification>> _classificationsByPosition =
		new(StringComparer.Ordinal);
	private readonly Dictionary<string, TaskCompletionSource<ImmutableDictionary<string, MoveClassification>>>
		_classificationCompletionByPosition = new(StringComparer.Ordinal);
	private readonly HashSet<string>                      _queuedClassificationPositions = new(StringComparer.Ordinal);
	private readonly int                                  _engineMoveTimeMs;
	private readonly List<PlayedMove>                     _moveHistory         = [];
	private readonly List<string>                         _playedMoves         = [];
	private readonly object                               _classificationSync  = new();
	private readonly Queue<PendingClassificationPosition> _classificationQueue = new();
	private readonly UciEngineClient                      _analysisClient;
	private readonly UciEngineClient                      _moveListClient;
	private readonly UciEngineClient                      _playingClient;
	private readonly UciPositionAnalysisCoordinator       _positionAnalysis;
	private          bool                                 _hasCurrentState;
	private          CancellationTokenSource              _classificationCts = new();
	private          Fen                                  _baseFen           = Fen.Default;
	private          PlayableMatchState                   _currentState;
	private          Task?                                _classificationWorker;

	/// <summary>
	///     Creates a playable match session.
	/// </summary>
	public UciPlayableMatchSession(
		UciEngineClient playingClient,
		UciEngineClient analysisClient,
		UciEngineClient moveListClient,
		char            playerColor,
		int             engineMoveTimeMs       = 1_000,
		int             moveListAnalysisTimeMs = 3_000,
		int             moveListFallbackTimeMs = 250)
	{
		_playingClient    = playingClient ?? throw new ArgumentNullException(nameof(playingClient));
		_analysisClient   = analysisClient ?? throw new ArgumentNullException(nameof(analysisClient));
		_moveListClient   = moveListClient ?? throw new ArgumentNullException(nameof(moveListClient));
		_engineMoveTimeMs = ValidatePositive(engineMoveTimeMs, nameof(engineMoveTimeMs));
		PlayerColor       = NormalizeColor(playerColor, nameof(playerColor));
		_positionAnalysis = new(
			_moveListClient,
			ValidatePositive(moveListAnalysisTimeMs, nameof(moveListAnalysisTimeMs)),
			ValidatePositive(moveListFallbackTimeMs, nameof(moveListFallbackTimeMs))
		);
	}

	/// <summary>
	///     Gets the engine's side.
	/// </summary>
	public char EngineColor => PlayerColor == 'w' ? 'b' : 'w';

	/// <summary>
	///     Gets the player's side: <c>w</c> or <c>b</c>.
	/// </summary>
	public char PlayerColor { get; }

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
		var classifications = GetKnownMoveClassifications(move.ParentPositionKey);
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
		GetKnownMoveClassifications(CurrentState.PositionKey);

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
	/// </summary>
	public async Task<EngineMoveResult> PlayEngineMoveAsync(CancellationToken ct = default)
	{
		var state = CurrentState;
		if (state.Fen.ActiveColor == PlayerColor)
			throw new InvalidOperationException("An engine move can only be played on the engine's turn.");

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
		Task<ImmutableDictionary<string, MoveClassification>> completionTask;

		lock (_classificationSync)
		{
			if (_classificationCompletionByPosition.TryGetValue(positionKey, out var completion))
				completionTask = completion.Task;
			else
				return GetKnownMoveClassifications(positionKey);
		}

		return await WaitWithCancellationAsync(completionTask, ct).ConfigureAwait(false);
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
		EnsureMoveClassifications(positionKey, fen.Value, legalMoves);
		EnrichMoveHistoryClassifications();

		if (legalMoves.Length > 0)
			_positionAnalysis.Enqueue(positionKey, _playedMoves, fen.Value.ActiveColor, PlayerColor, legalMoves);

		var advantage = _moveHistory.ResolveCurrentAdvantage(
			positionKey,
			legalMoves.Length,
			ResolvePositionAnalysis
		);

		var classifications = GetKnownMoveClassifications(positionKey);
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
	/// </summary>
	public void ApplyHumanMove(string move)
	{
		string normalizedMove = NormalizeMove(move);
		var    state          = CurrentState;

		if (state.Fen.ActiveColor != PlayerColor)
			throw new InvalidOperationException("A human move can only be applied on the player's turn.");

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
		ResetClassificationWorker();
	}

	private static char NormalizeColor(char color, string paramName)
	{
		if (color is 'w' or 'b')
			return color;

		throw new ArgumentOutOfRangeException(paramName, "Color must be 'w' or 'b'.");
	}

	private static ImmutableDictionary<string, MoveClassification> MergeClassifications(
		ImmutableDictionary<string, MoveClassification> structural,
		ImmutableDictionary<string, MoveClassification> existing)
	{
		if (existing.Count == 0)
			return structural;

		var builder = structural.ToBuilder();
		foreach ((string move, var classification) in existing)
		{
			if (!classification.IsResolved)
				continue;

			if (builder.ContainsKey(move))
				builder[move] = classification;
		}

		return builder.ToImmutable();
	}

	private static int ValidatePositive(int value, string paramName)
	{
		if (value > 0)
			return value;

		throw new ArgumentOutOfRangeException(paramName, "Value must be greater than zero.");
	}

	private static string NormalizeMove(string move)
	{
		if (move is null) throw new ArgumentNullException(nameof(move));

		string normalizedMove = move.Trim().ToLowerInvariant();
		if (!UciEngineClient.IsUciMoveString(normalizedMove))
			throw new ArgumentException("Enter a move in UCI notation such as e2e4 or a7a8q.", nameof(move));

		return normalizedMove;
	}

	private static async Task<T> WaitWithCancellationAsync<T>(Task<T> task, CancellationToken ct)
	{
#if NET9_0
		return await task.WaitAsync(ct).ConfigureAwait(false);
#else
		if (!ct.CanBeCanceled)
			return await task.ConfigureAwait(false);

		var cancellationTask = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		using var registration = ct.Register(
			static state => ((TaskCompletionSource<bool>)state!).TrySetResult(true),
			cancellationTask
		);

		if (task != await Task.WhenAny(task, cancellationTask.Task).ConfigureAwait(false))
			throw new OperationCanceledException(ct);

		return await task.ConfigureAwait(false);
#endif
	}

	private ImmutableDictionary<string, MoveClassification> GetKnownMoveClassifications(string positionKey)
	{
		lock (_classificationSync)
		{
			return _classificationsByPosition.TryGetValue(positionKey, out var classifications)
					   ? classifications
					   : ImmutableDictionary<string, MoveClassification>.Empty.WithComparers(StringComparer.Ordinal);
		}
	}

	private Task ClassifyPositionAsync(PendingClassificationPosition position, CancellationToken ct)
	{
		foreach (string move in position.LegalMoves)
		{
			ct.ThrowIfCancellationRequested();

			var resolved = GetKnownMoveClassifications(position.PositionKey).TryGetValue(move, out var current) &&
						   current.IsResolved
							   ? current
							   : position.Fen.ClassifyMoveFully(move);

			lock (_classificationSync)
			{
				var updated = GetKnownMoveClassifications(position.PositionKey).SetItem(move, resolved);
				_classificationsByPosition[position.PositionKey] = updated;
			}
		}

		lock (_classificationSync)
		{
			_queuedClassificationPositions.Remove(position.PositionKey);
			var completed = GetKnownMoveClassifications(position.PositionKey);
			GetOrCreateClassificationCompletion(position.PositionKey).TrySetResult(completed);
		}

		return Task.CompletedTask;
	}

	private async Task RunClassificationWorkerAsync(CancellationToken ct)
	{
		while (true)
		{
			PendingClassificationPosition position;
			lock (_classificationSync)
			{
				if (_classificationQueue.Count == 0)
				{
					_classificationWorker = null;
					return;
				}

				position = _classificationQueue.Dequeue();
			}

			try
			{
				await ClassifyPositionAsync(position, ct).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (ct.IsCancellationRequested)
			{
				return;
			}
			catch (Exception ex)
			{
				lock (_classificationSync)
				{
					_queuedClassificationPositions.Remove(position.PositionKey);
					GetOrCreateClassificationCompletion(position.PositionKey).TrySetException(ex);
				}

				throw;
			}
		}
	}

	private TaskCompletionSource<ImmutableDictionary<string, MoveClassification>> GetOrCreateClassificationCompletion(
		string positionKey)
	{
		if (_classificationCompletionByPosition.TryGetValue(positionKey, out var completion))
			return completion;

		completion                                       = new(TaskCreationOptions.RunContinuationsAsynchronously);
		_classificationCompletionByPosition[positionKey] = completion;
		return completion;
	}

	private void EnrichMoveHistoryClassifications()
	{
		for (var i = 0; i < _moveHistory.Count; i++)
		{
			var move            = _moveHistory[i];
			var classifications = GetKnownMoveClassifications(move.ParentPositionKey);
			if (!classifications.TryGetValue(move.Move, out var classification))
				continue;

			if (move.Classification == classification)
				continue;

			_moveHistory[i] = move with { Classification = classification };
		}
	}

	private void EnsureClassificationWorkerStarted()
	{
		if (_classificationWorker is { IsCompleted: false })
			return;

		_classificationWorker = RunClassificationWorkerAsync(_classificationCts.Token);
	}

	private void EnsureMoveClassifications(string positionKey, Fen fen, ImmutableArray<string> legalMoves)
	{
		var structural = fen.ClassifyMoves(legalMoves);
		lock (_classificationSync)
		{
			if (_classificationsByPosition.TryGetValue(positionKey, out var existing))
				structural = MergeClassifications(structural, existing);

			_classificationsByPosition[positionKey] = structural;

			var completion = GetOrCreateClassificationCompletion(positionKey);
			if (legalMoves.IsDefaultOrEmpty ||
				structural.Values.All(static classification => classification.IsResolved))
			{
				completion.TrySetResult(structural);
				return;
			}

			if (_queuedClassificationPositions.Add(positionKey))
			{
				_classificationQueue.Enqueue(new(positionKey, fen, legalMoves));
				EnsureClassificationWorkerStarted();
			}
		}
	}

	private void ResetBackgroundState()
	{
		_positionAnalysis.Cancel();
		ResetClassificationWorker();
		lock (_classificationSync)
		{
			_classificationsByPosition.Clear();
			_classificationCompletionByPosition.Clear();
			_classificationQueue.Clear();
			_queuedClassificationPositions.Clear();
		}
	}

	private void ResetClassificationWorker()
	{
		var cts = Interlocked.Exchange(ref _classificationCts, new());
		try
		{
			cts.Cancel();
		}
		finally
		{
			cts.Dispose();
		}

		lock (_classificationSync)
		{
			foreach (var completion in _classificationCompletionByPosition.Values)
				completion.TrySetCanceled();

			_classificationWorker = null;
		}
	}

	private void UpdateMoveHistory(string currentPositionKey)
	{
		if (_moveHistory.Count == _playedMoves.Count)
			return;

		if (_moveHistory.Count != _playedMoves.Count - 1)
			throw new InvalidOperationException("Move history can only be extended by one move per position snapshot.");

		int    moveIndex         = _moveHistory.Count;
		string parentPositionKey = moveIndex == 0 ? _baseFen.Raw : _moveHistory[^1].PositionKey;
		var classification = GetKnownMoveClassifications(parentPositionKey)
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

	private readonly record struct PendingClassificationPosition(
		string                 PositionKey,
		Fen                    Fen,
		ImmutableArray<string> LegalMoves
	);
}
