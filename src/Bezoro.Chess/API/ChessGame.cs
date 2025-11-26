using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Bezoro.Chess.API.Types;
using Bezoro.Chess.Internal;
using Bezoro.UCI.API;
using Bezoro.UCI.API.Types;

namespace Bezoro.Chess.API;

/// <summary>
///     Main facade for managing a chess game with engine integration.
///     Designed for Unity consumption as a .dll with events for UI updates.
/// </summary>
public sealed class ChessGame : IAsyncDisposable, IDisposable
{
	private readonly ChessGameOptions _options;
	private readonly GameHistory      _history        = new();
	private readonly object           _sync           = new();
	private readonly Stopwatch        _clockStopwatch = new();

	private readonly SynchronizationContext? _syncContext;
	private readonly UciCoordinator          _coordinator;
	private          bool                    _isEngineThinking;
	private          GameClock               _clock;

	private GameState _state;
	private Timer?    _clockTimer;

	/// <summary>
	///     Raised when the clock ticks (every 100ms while running).
	/// </summary>
	public event Action<GameClock>? ClockTick;

	/// <summary>
	///     Raised when the engine starts or stops thinking.
	/// </summary>
	public event Action<bool>? EngineThinking;

	/// <summary>
	///     Raised when an error occurs.
	/// </summary>
	public event Action<Exception>? Error;

	/// <summary>
	///     Raised when the game ends.
	/// </summary>
	public event Action<GameResult>? GameOver;

	/// <summary>
	///     Raised when a move is played (by player or engine).
	/// </summary>
	public event Action<ChessMove>? MovePlayed;

	/// <summary>
	///     Raised when the game state changes (position, evaluation, etc.).
	/// </summary>
	public event Action<GameState>? StateChanged;

	private ChessGame(
		ChessGameOptions        options,
		UciCoordinator          coordinator,
		SynchronizationContext? syncContext)
	{
		_options     = options;
		_coordinator = coordinator;
		_syncContext = syncContext;
		_clock       = options.EffectiveTimeControl;
		_state       = GameState.Default;

		// Wire up UCI coordinator events
		_coordinator.StateChanged += OnUciStateChanged;
		_coordinator.Error        += OnUciError;
	}

	/// <summary>
	///     Gets whether redo is available.
	/// </summary>
	public bool CanRedo => _history.CanRedo && !IsEngineThinking;

	/// <summary>
	///     Gets whether undo is available.
	/// </summary>
	public bool CanUndo => _history.CanUndo && !IsEngineThinking;

	/// <summary>
	///     Gets whether it's the engine's turn.
	/// </summary>
	public bool IsEngineTurn => State.SideToMove == EngineColor;

	/// <summary>
	///     Gets whether the game is over.
	/// </summary>
	public bool IsGameOver => State.IsGameOver;

	/// <summary>
	///     Gets whether it's the player's turn.
	/// </summary>
	public bool IsPlayerTurn => State.SideToMove == PlayerColor;

	/// <summary>
	///     Gets the game options.
	/// </summary>
	public ChessGameOptions Options => _options;

	/// <summary>
	///     Gets the last move played.
	/// </summary>
	public ChessMove? LastMove => State.LastMove;

	/// <summary>
	///     Gets the current FEN position.
	/// </summary>
	public Fen CurrentFen => State.CurrentFen;

	/// <summary>
	///     Gets the game result.
	/// </summary>
	public GameResult Result => State.Result;

	/// <summary>
	///     Gets the current evaluation in centipawns.
	/// </summary>
	public int? Evaluation => State.Evaluation;

	/// <summary>
	///     Gets the move history.
	/// </summary>
	public IReadOnlyList<ChessMove> History => _history.Moves;

	/// <summary>
	///     Gets the list of legal moves in the current position.
	/// </summary>
	public IReadOnlyList<string> LegalMoves => State.LegalMoves;

	/// <summary>
	///     Gets the engine's color.
	/// </summary>
	public PlayerColor EngineColor => _options.EngineColor;

	/// <summary>
	///     Gets the player's color.
	/// </summary>
	public PlayerColor PlayerColor => _options.PlayerColor;

	/// <summary>
	///     Gets the engine's current best move suggestion.
	/// </summary>
	public string? BestMove => State.BestMove;

	private bool IsAnalysisMode => !_options.AutoPlayEngineMove;

	/// <summary>
	///     Gets whether the engine is currently thinking.
	/// </summary>
	public bool IsEngineThinking
	{
		get
		{
			lock (_sync)
			{
				return _isEngineThinking;
			}
		}
		private set
		{
			bool changed;
			lock (_sync)
			{
				changed           = _isEngineThinking != value;
				_isEngineThinking = value;
			}

			if (changed) Raise(EngineThinking, value);
		}
	}

	/// <summary>
	///     Gets the current clock state.
	/// </summary>
	public GameClock Clock
	{
		get
		{
			lock (_sync)
			{
				return _clock;
			}
		}
		private set
		{
			lock (_sync)
			{
				_clock = value;
			}

			Raise(ClockTick, value);
		}
	}

	/// <summary>
	///     Gets the current game state.
	/// </summary>
	public GameState State
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
	///     Creates and starts a new chess game.
	/// </summary>
	/// <param name="options">Game configuration options.</param>
	/// <param name="syncContext">Optional synchronization context for events.</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>A fully initialized chess game.</returns>
	public static async Task<ChessGame> CreateAsync(
		ChessGameOptions        options,
		SynchronizationContext? syncContext = null,
		CancellationToken       ct          = default)
	{
		var uciOptions = new UciCoordinatorOptions(
			ClassificationDepth: options.ClassificationDepth
		);

		var coordinator = await UciCoordinator.CreateAsync(
							  options.EnginePath,
							  uciOptions,
							  syncContext,
							  ct
						  ).ConfigureAwait(false);

		var game = new ChessGame(options, coordinator, syncContext);
		await game.InitializeAsync(ct).ConfigureAwait(false);

		return game;
	}

	/// <summary>
	///     Offers or accepts a draw.
	/// </summary>
	/// <returns>True if draw was accepted/confirmed.</returns>
	public bool OfferDraw()
	{
		if (IsGameOver)
			return false;

		// In a single-player vs engine context, draw offer is just accepted
		EndGame(GameResult.DrawByAgreement);
		return true;
	}

	/// <summary>
	///     Gets the 8x8 board representation for Unity rendering.
	/// </summary>
	/// <returns>A 2D array of piece characters (uppercase=white, lowercase=black, null=empty).</returns>
	public char?[,] GetBoardView() => BoardViewBuilder.Build(State.CurrentFen);

	/// <summary>
	///     Streams moves as they are played.
	/// </summary>
	public async IAsyncEnumerable<ChessMove> StreamMovesAsync(
		[EnumeratorCancellation] CancellationToken ct = default)
	{
		var channel = Channel.CreateUnbounded<ChessMove>();

		void OnMovePlayed(ChessMove move)
		{
			channel.Writer.TryWrite(move);
		}

		void OnGameOver(GameResult _)
		{
			channel.Writer.TryComplete();
		}

		MovePlayed += OnMovePlayed;
		GameOver   += OnGameOver;

		try
		{
			while (await channel.Reader.WaitToReadAsync(ct).ConfigureAwait(false))
			{
				while (channel.Reader.TryRead(out var move))
					yield return move;
			}
		}
		finally
		{
			MovePlayed -= OnMovePlayed;
			GameOver   -= OnGameOver;
			channel.Writer.TryComplete();
		}
	}

	/// <summary>
	///     Streams game state changes.
	/// </summary>
	public async IAsyncEnumerable<GameState> StreamStateAsync(
		[EnumeratorCancellation] CancellationToken ct = default)
	{
		var channel = Channel.CreateUnbounded<GameState>();

		void OnStateChanged(GameState state)
		{
			channel.Writer.TryWrite(state);
		}

		StateChanged += OnStateChanged;

		try
		{
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
	///     Gets the classification for a specific move.
	/// </summary>
	public MoveClassification GetMoveClassification(string moveNotation) =>
		State.GetMoveClassification(moveNotation);

	/// <summary>
	///     Exports the game to PGN format.
	/// </summary>
	public string ToPgn() => PgnExporter.Export(this);

	/// <summary>
	///     Starts a new game with the same options.
	/// </summary>
	/// <param name="ct">Cancellation token.</param>
	public async Task NewGameAsync(CancellationToken ct = default)
	{
		StopClock();
		_history.Clear();

		await _coordinator.NewGameAsync(ct).ConfigureAwait(false);
		await InitializeAsync(ct).ConfigureAwait(false);
	}

	/// <summary>
	///     Starts a new game with different options.
	/// </summary>
	/// <param name="options">New game options.</param>
	/// <param name="ct">Cancellation token.</param>
	public async Task NewGameAsync(ChessGameOptions options, CancellationToken ct = default)
	{
		// This would require creating a new coordinator if engine path differs
		// For simplicity, just reset with current options
		await NewGameAsync(ct).ConfigureAwait(false);
	}

	/// <summary>
	///     Plays a move for the human player.
	/// </summary>
	/// <param name="moveNotation">The move in UCI notation (e.g., "e2e4").</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>True if the move was legal and played, false otherwise.</returns>
	public async Task<bool> PlayMoveAsync(string moveNotation, CancellationToken ct = default)
	{
		if (IsGameOver)
			return false;

		if (!IsPlayerTurn && !IsAnalysisMode)
			return false;

		// Validate the move is legal
		if (!State.LegalMoves.Contains(moveNotation))
			return false;

		await PlayMoveInternalAsync(moveNotation, State.SideToMove, ct).ConfigureAwait(false);

		// If auto-play is enabled and it's now the engine's turn, trigger engine move
		if (_options.AutoPlayEngineMove && IsEngineTurn && !IsGameOver) _ = RequestEngineMoveAsync(ct);

		return true;
	}

	/// <summary>
	///     Redoes previously undone move(s).
	/// </summary>
	/// <param name="count">Number of moves to redo.</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>True if redo was successful.</returns>
	public async Task<bool> RedoAsync(int count = 1, CancellationToken ct = default)
	{
		if (!CanRedo || IsEngineThinking)
			return false;

		var entries = _history.Redo(count);
		if (entries.Count == 0)
			return false;

		// Replay moves through the coordinator
		foreach (var entry in entries)
		{
			await _coordinator.MakeMoveAsync(entry.Move.Notation, ct).ConfigureAwait(false);
			Clock = Clock.SwitchTurn(entry.Move.MovingColor);
		}

		return true;
	}

	/// <summary>
	///     Undoes the last move(s).
	/// </summary>
	/// <param name="count">Number of moves to undo.</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>True if undo was successful.</returns>
	public async Task<bool> UndoAsync(int count = 1, CancellationToken ct = default)
	{
		if (!CanUndo || IsEngineThinking)
			return false;

		var entries = _history.Undo(count);
		if (entries.Count == 0)
			return false;

		// Restore clock state
		if (_history.Count > 0)
		{
			var lastClock = _history.GetClockBefore(_history.Count - 1);
			Clock = lastClock.SwitchTurn(_history.GetMove(_history.Count - 1).MovingColor);
		}
		else
		{
			Clock = _options.EffectiveTimeControl;
		}

		// Update position in coordinator
		await _coordinator.UndoAsync(entries.Count, ct).ConfigureAwait(false);

		return true;
	}

	/// <summary>
	///     Requests the engine to make a move.
	/// </summary>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>The move played by the engine, or null if not engine's turn or game over.</returns>
	public async Task<ChessMove?> RequestEngineMoveAsync(CancellationToken ct = default)
	{
		if (IsGameOver)
			return null;

		if (!IsEngineTurn && !IsAnalysisMode)
			return null;

		IsEngineThinking = true;
		try
		{
			// Wait for a good move from the engine
			var searchParams = new SearchParameters
			{
				Depth      = _options.EngineDepth,
				MoveTimeMs = _options.EngineDepth.HasValue ? null : _options.EngineThinkTimeMs
			};

			var result = await _coordinator.SearchAsync(searchParams, ct).ConfigureAwait(false);

			if (!string.IsNullOrEmpty(result.BestMove))
			{
				await PlayMoveInternalAsync(result.BestMove, State.SideToMove, ct).ConfigureAwait(false);
				return _history.LastMove;
			}

			return null;
		}
		finally
		{
			IsEngineThinking = false;
		}
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		StopClock();
		_clockTimer?.Dispose();

		_coordinator.StateChanged -= OnUciStateChanged;
		_coordinator.Error        -= OnUciError;

		await _coordinator.DisposeAsync().ConfigureAwait(false);
	}

	/// <inheritdoc />
	public void Dispose()
	{
		DisposeAsync().AsTask().GetAwaiter().GetResult();
	}

	/// <summary>
	///     Resigns the game for the player.
	/// </summary>
	public void Resign()
	{
		if (IsGameOver)
			return;

		var result = GameResult.Resignation(PlayerColor.Opponent());
		EndGame(result);
	}

	private GameResult DetermineResult(UciState uciState)
	{
		if (uciState.IsCheckmate)
		{
			var loser = uciState.CurrentFen.ActiveColor == 'w' ? PlayerColor.White : PlayerColor.Black;
			return GameResult.Checkmate(loser.Opponent());
		}

		if (uciState.IsStalemate)
			return GameResult.Stalemate;

		if (Clock.IsTimeout)
		{
			var loser = Clock.TimedOutPlayer!.Value;
			return GameResult.Timeout(loser.Opponent());
		}

		return GameResult.Ongoing;
	}

	private async Task InitializeAsync(CancellationToken ct)
	{
		// Set starting position
		if (_options.StartingFen != null)
			await _coordinator.SetPositionAsync(_options.StartingFen, ct).ConfigureAwait(false);
		else
			await _coordinator.ResetAsync(ct).ConfigureAwait(false);

		// Initialize clock
		_clock = _options.EffectiveTimeControl.SetActiveColor(PlayerColor.White);

		// Update state
		UpdateStateFromCoordinator();

		// If player is black and auto-play is on, start engine move
		if (PlayerColor == PlayerColor.Black && _options.AutoPlayEngineMove) _ = RequestEngineMoveAsync(ct);
	}

	private async Task PlayMoveInternalAsync(string notation, PlayerColor movingColor, CancellationToken ct)
	{
		// Record clock state before move
		var clockBefore = Clock;

		// Make the move
		await _coordinator.MakeMoveAsync(notation, ct).ConfigureAwait(false);

		// Create chess move from the played move
		var move = ChessMove.FromNotation(notation, movingColor);

		// Update clock
		Clock = Clock.SwitchTurn(movingColor);

		// Record in history
		_history.AddMove(move, clockBefore);

		// Raise move played event
		Raise(MovePlayed, move);

		// Check for game end conditions
		CheckGameEndConditions();
	}

	private void CheckGameEndConditions()
	{
		var uciState = _coordinator.State;

		if (uciState.IsCheckmate)
		{
			var winner = State.SideToMove.Opponent();
			EndGame(GameResult.Checkmate(winner));
		}
		else if (uciState.IsStalemate)
		{
			EndGame(GameResult.Stalemate);
		}
		else if (Clock.IsTimeout)
		{
			var winner = Clock.TimedOutPlayer!.Value.Opponent();
			EndGame(GameResult.Timeout(winner));
		}
	}

	private void EndGame(GameResult result)
	{
		StopClock();

		lock (_sync)
		{
			_state = _state with { Result = result };
		}

		Raise(StateChanged, _state);
		Raise(GameOver,     result);
	}

	private void OnClockTick()
	{
		long elapsed = _clockStopwatch.ElapsedMilliseconds;
		_clockStopwatch.Restart();

		Clock = _clock.Tick(elapsed);

		if (_clock.IsTimeout) CheckGameEndConditions();
	}

	private void OnUciError(Exception ex)
	{
		Raise(Error, ex);
	}

	private void OnUciStateChanged(UciState uciState)
	{
		var result = DetermineResult(uciState);

		lock (_sync)
		{
			_state = GameState.FromUciState(uciState, _history.MovesImmutable, result);
		}

		Raise(StateChanged, _state);
	}

	private void Raise<T>(Action<T>? handler, T arg)
	{
		if (handler == null) return;

		if (_syncContext != null)
			_syncContext.Post(_ => handler(arg), null);
		else
			handler(arg);
	}

	private void StartClock()
	{
		if (_clock.IsUnlimited)
			return;

		_clockStopwatch.Restart();
		Clock = _clock.Start();

		_clockTimer = new(
			_ => OnClockTick(),
			null,
			100,
			100
		);
	}

	private void StopClock()
	{
		_clockTimer?.Change(Timeout.Infinite, Timeout.Infinite);
		_clockTimer?.Dispose();
		_clockTimer = null;

		_clockStopwatch.Stop();

		if (!_clock.IsUnlimited) Clock = _clock.Stop();
	}

	private void UpdateStateFromCoordinator()
	{
		var uciState = _coordinator.State;
		var result   = DetermineResult(uciState);

		lock (_sync)
		{
			_state = GameState.FromUciState(uciState, _history.MovesImmutable, result);
		}
	}
}
