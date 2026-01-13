using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Bezoro.Chess.API.Abstractions;
using Bezoro.Chess.API.Opponents;
using Bezoro.Chess.API.Types;
using Bezoro.Chess.Internal;
using Bezoro.UCI.API;
using Bezoro.UCI.API.Types;

namespace Bezoro.Chess.API;

/// <summary>
///     Main facade for managing a chess game with multiple opponent types.
///     Supports engine, local human (same device), and online opponents.
///     Designed for Unity consumption as a .dll with events for UI updates.
/// </summary>
public sealed class ChessGame : IAsyncDisposable, IDisposable
{
	private readonly ChessGameOptions         _options;
	private readonly Dictionary<string, char> _promotionChoices = new();
	private readonly GameHistory              _history          = new();
	private readonly object                   _sync             = new();
	private readonly Stopwatch                _clockStopwatch   = new();

	private readonly SynchronizationContext? _syncContext;
	private readonly UciCoordinator?         _coordinator;

	private bool      _isOpponentThinking;
	private GameClock _clock;
	private GameState _state;
	private Timer?    _clockTimer;

	/// <summary>
	///     Raised when the clock ticks (every 100ms while running).
	/// </summary>
	public event Action<GameClock>? ClockTick;

	/// <summary>
	///     Raised when a draw offer is received from the opponent.
	/// </summary>
	public event Action? DrawOfferReceived;

	/// <summary>
	///     Raised when an error occurs.
	/// </summary>
	public event Action<Exception>? Error;

	/// <summary>
	///     Raised when the game ends.
	/// </summary>
	public event Action<GameResult>? GameOver;

	/// <summary>
	///     Raised when the game starts (after initialization is complete).
	///     This event fires when a new game begins, including when NewGameAsync is called.
	/// </summary>
	public event Action? GameStarted;

	/// <summary>
	///     Raised when a move is played (by player or opponent).
	/// </summary>
	public event Action<ChessMove>? MovePlayed;

	/// <summary>
	///     Raised when the opponent disconnects (online games only).
	/// </summary>
	public event Action? OpponentDisconnected;

	/// <summary>
	///     Raised when the opponent starts or stops thinking.
	/// </summary>
	public event Action<bool>? OpponentThinking;

	/// <summary>
	///     Raised when a pawn reaches a promotion square (rank 7 for white, rank 2 for black).
	///     The event provides the square where the pawn is located.
	/// </summary>
	public event Action<PromotionInfo>? PawnReachedPromotionSquare;

	/// <summary>
	///     Raised when a promotion piece choice is set for a pawn.
	///     The event provides the square and the chosen promotion piece.
	/// </summary>
	public event Action<PromotionChoiceInfo>? PromotionChoiceSet;

	/// <summary>
	///     Raised when the game state changes (position, evaluation, etc.).
	/// </summary>
	public event Action<GameState>? StateChanged;

	private ChessGame(
		ChessGameOptions        options,
		IOpponent               opponent,
		UciCoordinator?         coordinator,
		SynchronizationContext? syncContext)
	{
		_options     = options;
		Opponent     = opponent;
		_coordinator = coordinator;
		_syncContext = syncContext;
		_clock       = options.EffectiveTimeControl;
		_state       = GameState.Default;

		// Wire up UCI coordinator events (if we have one)
		if (_coordinator != null)
		{
			_coordinator.StateChanged += OnUciStateChanged;
			_coordinator.Error        += OnError;
		}

		// Wire up opponent events
		Opponent.MoveSubmitted += OnOpponentMoveSubmitted;
		Opponent.DrawOffered   += OnOpponentDrawOffered;
		Opponent.Resigned      += OnOpponentResigned;
		Opponent.Disconnected  += OnOpponentDisconnected;
		Opponent.Error         += OnError;
	}

	// ============ Properties ============

	/// <summary>
	///     Gets whether redo is available.
	/// </summary>
	public bool CanRedo => _history.CanRedo && !IsOpponentThinking;

	/// <summary>
	///     Gets whether undo is available.
	/// </summary>
	public bool CanUndo => _history.CanUndo && !IsOpponentThinking;

	/// <summary>
	///     Gets whether the game is over.
	/// </summary>
	public bool IsGameOver => State.IsGameOver;

	/// <summary>
	///     Gets whether it's the opponent's turn.
	/// </summary>
	public bool IsOpponentTurn => State.SideToMove == OpponentColor;

	/// <summary>
	///     Gets whether it's the local player's turn.
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
	///     Gets the opponent.
	/// </summary>
	public IOpponent Opponent { get; }

	/// <summary>
	///     Gets the move history.
	/// </summary>
	public IReadOnlyList<ChessMove> History => _history.Moves;

	/// <summary>
	///     Gets the list of legal moves in the current position.
	/// </summary>
	public IReadOnlyList<string> LegalMoves => State.LegalMoves;

	/// <summary>
	///     Gets the opponent type.
	/// </summary>
	public OpponentType OpponentType => _options.OpponentType;

	/// <summary>
	///     Gets the opponent's color.
	/// </summary>
	public PlayerColor OpponentColor => _options.OpponentColor;

	/// <summary>
	///     Gets the local player's color.
	/// </summary>
	public PlayerColor PlayerColor => _options.PlayerColor;

	/// <summary>
	///     Gets the opponent's profile.
	/// </summary>
	public PlayerProfile OpponentProfile => Opponent.Profile;

	/// <summary>
	///     Gets the local player's profile.
	/// </summary>
	public PlayerProfile? LocalPlayer => _options.LocalPlayer;

	/// <summary>
	///     Gets the engine's current best move suggestion (engine games only).
	/// </summary>
	public string? BestMove => State.BestMove;

	private bool IsAnalysisMode => !_options.AutoPlayOpponentMove;

	/// <summary>
	///     Gets whether the opponent is currently thinking.
	/// </summary>
	public bool IsOpponentThinking
	{
		get
		{
			lock (_sync)
			{
				return _isOpponentThinking;
			}
		}
		private set
		{
			bool changed;
			lock (_sync)
			{
				changed             = _isOpponentThinking != value;
				_isOpponentThinking = value;
			}

			if (changed) Raise(OpponentThinking, value);
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

	// ============ Factory Methods ============

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
		UciCoordinator? coordinator = null;
		IOpponent       opponent;

		switch (options.OpponentType)
		{
			case OpponentType.Engine:
				// Create engine opponent and UCI coordinator
				if (string.IsNullOrEmpty(options.EnginePath))
					throw new ArgumentException("EnginePath is required for engine games.", nameof(options));

				var uciOptions = new UciCoordinatorOptions(
					ClassificationDepth: options.ClassificationDepth
				);

				coordinator = await UciCoordinator.CreateAsync(
								  options.EnginePath,
								  uciOptions,
								  syncContext,
								  ct
							  ).ConfigureAwait(false);

				opponent = new EngineOpponent(
					options.EnginePath,
					options.EngineDifficulty,
					"Chess Engine"
				);

				await opponent.InitializeAsync(ct).ConfigureAwait(false);
				break;

			case OpponentType.LocalHuman:
				// Create local human opponent
				if (string.IsNullOrEmpty(options.EnginePath))
					throw new ArgumentException(
						"EnginePath is required for all game types (needed for legal moves, classification, and evaluation).",
						nameof(options));

				var localOpponentProfile = options.LocalOpponent ?? PlayerProfile.Create("Player 2");
				opponent = new LocalHumanOpponent(localOpponentProfile);
				await opponent.InitializeAsync(ct).ConfigureAwait(false);

				// Engine is required for legal moves, classification, and evaluation
				var localUciOptions = new UciCoordinatorOptions(ClassificationDepth: options.ClassificationDepth);
				coordinator = await UciCoordinator.CreateAsync(
								  options.EnginePath,
								  localUciOptions,
								  syncContext,
								  ct
							  ).ConfigureAwait(false);

				break;

			case OpponentType.RemoteHuman:
				// Create remote opponent
				if (options.RemoteService == null)
					throw new ArgumentException("RemoteService is required for online games.", nameof(options));

				if (string.IsNullOrEmpty(options.EnginePath))
					throw new ArgumentException(
						"EnginePath is required for all game types (needed for legal moves, classification, and evaluation).",
						nameof(options));

				var localPlayer = options.LocalPlayer ?? PlayerProfile.Create("Player");
				var match       = await options.RemoteService.FindMatchAsync(localPlayer, ct).ConfigureAwait(false);

				if (!match.HasValue)
					throw new InvalidOperationException("Failed to find a match.");

				opponent = new RemoteOpponent(options.RemoteService, match.Value);
				await opponent.InitializeAsync(ct).ConfigureAwait(false);

				// Engine is required for legal moves, classification, and evaluation
				var remoteUciOptions = new UciCoordinatorOptions(ClassificationDepth: options.ClassificationDepth);
				coordinator = await UciCoordinator.CreateAsync(
								  options.EnginePath,
								  remoteUciOptions,
								  syncContext,
								  ct
							  ).ConfigureAwait(false);

				break;

			default:
				throw new ArgumentOutOfRangeException(nameof(options), "Unknown opponent type.");
		}

		var game = new ChessGame(options, opponent, coordinator, syncContext);
		await game.InitializeAsync(ct).ConfigureAwait(false);

		return game;
	}

	/// <summary>
	///     Creates a local two-player game.
	///     The engine is required for legal move calculation, move classification, and evaluation.
	/// </summary>
	/// <param name="enginePath">Path to the UCI engine executable (required for legal moves and analysis).</param>
	/// <param name="player1Name">First player's name.</param>
	/// <param name="player2Name">Second player's name.</param>
	/// <param name="syncContext">Optional synchronization context for events.</param>
	/// <param name="ct">Cancellation token.</param>
	public static Task<ChessGame> CreateLocalTwoPlayerAsync(
		string                  enginePath,
		string                  player1Name,
		string                  player2Name,
		SynchronizationContext? syncContext = null,
		CancellationToken       ct          = default)
	{
		var options = ChessGameOptions.LocalTwoPlayer(enginePath, player1Name, player2Name);
		return CreateAsync(options, syncContext, ct);
	}

	/// <summary>
	///     Creates an online multiplayer game.
	///     The engine is required for legal move calculation, move classification, and evaluation.
	/// </summary>
	/// <param name="enginePath">Path to the UCI engine executable (required for legal moves and analysis).</param>
	/// <param name="remoteService">The remote game service implementation.</param>
	/// <param name="localPlayer">The local player's profile.</param>
	/// <param name="yourColor">Your assigned color.</param>
	/// <param name="opponentProfile">The opponent's profile.</param>
	/// <param name="timeControl">Optional time control.</param>
	/// <param name="syncContext">Optional synchronization context for events.</param>
	/// <param name="ct">Cancellation token.</param>
	public static Task<ChessGame> CreateOnlineMultiplayerAsync(
		string                  enginePath,
		IRemoteGameService      remoteService,
		PlayerProfile           localPlayer,
		PlayerColor             yourColor,
		PlayerProfile           opponentProfile,
		GameClock?              timeControl = null,
		SynchronizationContext? syncContext = null,
		CancellationToken       ct          = default)
	{
		var options = ChessGameOptions.OnlineMultiplayer(
			enginePath,
			remoteService,
			localPlayer,
			yourColor,
			opponentProfile,
			timeControl);

		return CreateAsync(options, syncContext, ct);
	}

	/// <summary>
	///     Creates a game against the engine with simplified options.
	/// </summary>
	public static Task<ChessGame> CreateVsEngineAsync(
		string                  enginePath,
		EngineDifficulty?       difficulty  = null,
		PlayerColor             playerColor = PlayerColor.White,
		SynchronizationContext? syncContext = null,
		CancellationToken       ct          = default)
	{
		var options = ChessGameOptions.VsEngine(enginePath, difficulty, playerColor);
		return CreateAsync(options, syncContext, ct);
	}

	/// <summary>
	///     Offers or accepts a draw.
	/// </summary>
	/// <returns>True if draw was accepted/confirmed.</returns>
	public bool OfferDraw()
	{
		if (IsGameOver)
			return false;

		// For engine games, draw is just accepted
		if (OpponentType == OpponentType.Engine)
		{
			EndGame(GameResult.DrawByAgreement);
			return true;
		}

		// For human opponents, we need to send the offer
		if (Opponent is RemoteOpponent remoteOpponent)
		{
			_ = remoteOpponent.OfferDrawAsync();
			return false; // Draw is not confirmed yet
		}

		// For local human, we can just accept
		EndGame(GameResult.DrawByAgreement);
		return true;
	}

	/// <summary>
	///     Sets the promotion piece choice for a pawn on a promotion square.
	///     The choice will be applied when the promotion move is played.
	/// </summary>
	/// <param name="fromSquare">The square where the pawn is located (e.g., "e7").</param>
	/// <param name="promotionPiece">The piece to promote to: 'q' (queen), 'r' (rook), 'b' (bishop), or 'n' (knight).</param>
	/// <returns>True if the choice was set, false if the square is invalid or piece is invalid.</returns>
	public bool SetPromotionChoice(string fromSquare, char promotionPiece)
	{
		if (string.IsNullOrEmpty(fromSquare) || fromSquare.Length < 2)
			return false;

		// Normalize promotion piece to lowercase
		promotionPiece = char.ToLowerInvariant(promotionPiece);

		// Validate promotion piece
		if (promotionPiece is not ('q' or 'r' or 'b' or 'n'))
			return false;

		// Normalize square to lowercase
		string normalizedSquare = fromSquare.ToLowerInvariant();

		var wasNewChoice = false;
		lock (_sync)
		{
			wasNewChoice = !_promotionChoices.ContainsKey(normalizedSquare) ||
						   _promotionChoices[normalizedSquare] != promotionPiece;

			_promotionChoices[normalizedSquare] = promotionPiece;
		}

		// Raise event when choice is set (or changed)
		if (wasNewChoice) Raise(PromotionChoiceSet, new(normalizedSquare, promotionPiece));

		return true;
	}

	/// <summary>
	///     Submits a move for the opponent (for local two-player games).
	/// </summary>
	/// <param name="moveNotation">The move in UCI notation.</param>
	/// <returns>True if the move was accepted.</returns>
	public bool SubmitOpponentMove(string moveNotation)
	{
		if (OpponentType != OpponentType.LocalHuman)
			return false;

		if (Opponent is LocalHumanOpponent localOpponent)
			return localOpponent.SubmitMove(moveNotation);

		return false;
	}

	/// <summary>
	///     Gets the promotion piece choice for a pawn on a promotion square.
	/// </summary>
	/// <param name="fromSquare">The square where the pawn is located (e.g., "e7").</param>
	/// <returns>The promotion piece choice ('q', 'r', 'b', or 'n'), or null if not set.</returns>
	public char? GetPromotionChoice(string fromSquare)
	{
		if (string.IsNullOrEmpty(fromSquare))
			return null;

		string normalizedSquare = fromSquare.ToLowerInvariant();

		lock (_sync)
		{
			return _promotionChoices.TryGetValue(normalizedSquare, out char choice) ? choice : null;
		}
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

		void OnMovePlayed(ChessMove move) => channel.Writer.TryWrite(move);
		void OnGameOver(GameResult  _)    => channel.Writer.TryComplete();

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

		void OnStateChanged(GameState state) => channel.Writer.TryWrite(state);

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
	///     Accepts a draw offer from the opponent.
	/// </summary>
	public async Task AcceptDrawAsync(CancellationToken ct = default)
	{
		if (IsGameOver)
			return;

		if (Opponent is RemoteOpponent remoteOpponent)
			await remoteOpponent.AcceptDrawAsync(ct).ConfigureAwait(false);

		EndGame(GameResult.DrawByAgreement);
	}

	/// <summary>
	///     Declines a draw offer from the opponent.
	/// </summary>
	public async Task DeclineDrawAsync(CancellationToken ct = default)
	{
		if (Opponent is RemoteOpponent remoteOpponent)
			await remoteOpponent.DeclineDrawAsync(ct).ConfigureAwait(false);
	}

	/// <summary>
	///     Starts a new game with the same options.
	/// </summary>
	/// <param name="ct">Cancellation token.</param>
	public async Task NewGameAsync(CancellationToken ct = default)
	{
		StopClock();
		_history.Clear();
		lock (_sync)
		{
			_promotionChoices.Clear();
		}

		if (_coordinator != null)
			await _coordinator.NewGameAsync(ct).ConfigureAwait(false);

		await InitializeAsync(ct).ConfigureAwait(false);
	}

	/// <summary>
	///     Plays a move for the current player.
	/// </summary>
	/// <param name="moveNotation">The move in UCI notation (e.g., "e2e4" or "e7e8" for promotion).</param>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>True if the move was legal and played, false otherwise.</returns>
	public async Task<bool> PlayMoveAsync(string moveNotation, CancellationToken ct = default)
	{
		if (IsGameOver)
			return false;

		// For local two-player or analysis mode, either player can move
		if (!IsPlayerTurn && !IsAnalysisMode && OpponentType != OpponentType.LocalHuman)
			return false;

		// Handle promotion moves - if move is 4 characters and pawn is on promotion square, append promotion piece
		string? processedNotation = ProcessPromotionMove(moveNotation);
		if (processedNotation == null)
			return false;

		// Validate the move is legal
		if (!State.LegalMoves.Contains(processedNotation))
			return false;

		await PlayMoveInternalAsync(processedNotation, State.SideToMove, ct).ConfigureAwait(false);

		// If auto-play is enabled and it's now the opponent's turn, trigger opponent move
		if (_options.AutoPlayOpponentMove && IsOpponentTurn && !IsGameOver)
			_ = RequestOpponentMoveAsync(ct);

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
		if (!CanRedo || IsOpponentThinking)
			return false;

		var entries = _history.Redo(count);
		if (entries.Count == 0)
			return false;

		// Replay moves through the coordinator
		foreach (var entry in entries)
		{
			if (_coordinator != null)
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
		if (!CanUndo || IsOpponentThinking)
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
			// Back to the initial clock state: white to move.
			Clock = _options.EffectiveTimeControl.SetActiveColor(PlayerColor.White).Start();
			StartClock();
		}

		// Update position in coordinator
		if (_coordinator != null)
			await _coordinator.UndoAsync(entries.Count, ct).ConfigureAwait(false);

		return true;
	}

	/// <summary>
	///     Requests the opponent to make a move.
	/// </summary>
	/// <param name="ct">Cancellation token.</param>
	/// <returns>The move played by the opponent, or null if not opponent's turn or game over.</returns>
	public async Task<ChessMove?> RequestOpponentMoveAsync(CancellationToken ct = default)
	{
		if (IsGameOver)
			return null;

		if (!IsOpponentTurn && !IsAnalysisMode)
			return null;

		IsOpponentThinking = true;
		try
		{
			// Get move from opponent
			string? move = await Opponent.GetMoveAsync(State, ct).ConfigureAwait(false);

			if (!string.IsNullOrEmpty(move))
			{
				await PlayMoveInternalAsync(move, State.SideToMove, ct).ConfigureAwait(false);
				return _history.LastMove;
			}

			return null;
		}
		finally
		{
			IsOpponentThinking = false;
		}
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		StopClock();
		_clockTimer?.Dispose();

		// Unsubscribe from events
		if (_coordinator != null)
		{
			_coordinator.StateChanged -= OnUciStateChanged;
			_coordinator.Error        -= OnError;
			await _coordinator.DisposeAsync().ConfigureAwait(false);
		}

		Opponent.MoveSubmitted -= OnOpponentMoveSubmitted;
		Opponent.DrawOffered   -= OnOpponentDrawOffered;
		Opponent.Resigned      -= OnOpponentResigned;
		Opponent.Disconnected  -= OnOpponentDisconnected;
		Opponent.Error         -= OnError;
		await Opponent.DisposeAsync().ConfigureAwait(false);
	}

	/// <inheritdoc />
	public void Dispose()
	{
		DisposeAsync().AsTask().GetAwaiter().GetResult();
	}

	/// <summary>
	///     Resigns the game for the local player.
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

	/// <summary>
	///     Processes a move notation, adding promotion piece if needed.
	///     The promotion choice (set via SetPromotionChoice) is applied here and will be sent to the engine
	///     when the move is played via MakeMoveAsync.
	/// </summary>
	private string? ProcessPromotionMove(string moveNotation)
	{
		if (string.IsNullOrEmpty(moveNotation))
			return null;

		// If move already has 5 characters, it already includes promotion piece
		if (moveNotation.Length == 5)
			return moveNotation;

		// If move is 4 characters, check if it's a promotion move
		if (moveNotation.Length == 4)
		{
			string fromSquare = moveNotation[..2].ToLowerInvariant();
			string toSquare   = moveNotation[2..].ToLowerInvariant();

			// Check if this is a potential promotion move (to rank 8 for white, rank 1 for black)
			char toRank            = toSquare[1];
			bool isPromotionSquare = toRank == '8' || toRank == '1';

			if (isPromotionSquare)
			{
				// Check if there's a pawn on the from square
				char?[,] board = BoardViewBuilder.Build(State.CurrentFen);
				char?    piece = BoardViewBuilder.GetPieceAt(board, fromSquare);

				if (piece.HasValue && char.ToLowerInvariant(piece.Value) == 'p')
				{
					// Get promotion choice (set via SetPromotionChoice) or default to queen
					// This choice will be applied to the engine when the move is played
					char promotionChoice = GetPromotionChoice(fromSquare) ?? 'q';
					return moveNotation + promotionChoice;
				}
			}
		}

		return moveNotation;
	}

	private async Task InitializeAsync(CancellationToken ct)
	{
		StopClock();
		lock (_sync)
		{
			_promotionChoices.Clear();
			_isOpponentThinking = false;
		}

		// Set starting position
		if (_coordinator != null)
		{
			if (_options.StartingFen != null)
				await _coordinator.SetPositionAsync(_options.StartingFen, ct).ConfigureAwait(false);
			else
				await _coordinator.ResetAsync(ct).ConfigureAwait(false);
		}

		// Initialize clock
		Clock = _options.EffectiveTimeControl.SetActiveColor(PlayerColor.White).Start();
		StartClock();

		// Update state
		UpdateStateFromCoordinator();

		// Check for pawns on promotion squares (in case starting position has pawns on rank 7/2)
		CheckForPromotionSquares();

		// Raise game started event
		Raise(GameStarted);

		// If player is black (or opponent moves first) and auto-play is on, start opponent move
		if (PlayerColor == PlayerColor.Black && _options.AutoPlayOpponentMove)
			_ = RequestOpponentMoveAsync(ct);
	}

	private async Task PlayMoveInternalAsync(string notation, PlayerColor movingColor, CancellationToken ct)
	{
		// Record clock state before move
		var clockBefore = Clock;

		// Make the move in coordinator (promotion piece is included in notation if applicable)
		// The UCI protocol requires promotions in format "e7e8q" where 'q' is the promotion piece
		if (_coordinator != null)
			await _coordinator.MakeMoveAsync(notation, ct).ConfigureAwait(false);

		// Create chess move from the played move
		var move = ChessMove.FromNotation(notation, movingColor);

		// Clear promotion choices for the from square (if it was a promotion move)
		if (move.Classification == MoveClassification.Promotion)
			lock (_sync)
			{
				_promotionChoices.Remove(move.FromSquare.ToLowerInvariant());
			}

		// Update clock
		Clock = Clock.SwitchTurn(movingColor);

		// Record in history
		_history.AddMove(move, clockBefore);

		// Notify opponent of the move
		await Opponent.NotifyMovePlayedAsync(notation, State, ct).ConfigureAwait(false);

		// Raise move played event
		Raise(MovePlayed, move);

		// Check for game end conditions
		CheckGameEndConditions();
	}

	/// <summary>
	///     Checks for pawns on promotion squares and raises the event if found.
	///     A pawn is on a promotion square when it's on rank 7 (white) or rank 2 (black).
	/// </summary>
	private void CheckForPromotionSquares()
	{
		if (IsGameOver)
			return;

		char?[,] board = BoardViewBuilder.Build(State.CurrentFen);

		// Check rank 7 for white pawns (white can promote from rank 7 to rank 8)
		for (var file = 0; file < 8; file++)
		{
			string square = BoardViewBuilder.GetSquare(file, 6); // Rank 7 (index 6)
			char?  piece  = BoardViewBuilder.GetPieceAt(board, square);

			if (piece == 'P') // White pawn
				Raise(PawnReachedPromotionSquare, new(square, PlayerColor.White));
		}

		// Check rank 2 for black pawns (black can promote from rank 2 to rank 1)
		for (var file = 0; file < 8; file++)
		{
			string square = BoardViewBuilder.GetSquare(file, 1); // Rank 2 (index 1)
			char?  piece  = BoardViewBuilder.GetPieceAt(board, square);

			if (piece == 'p') // Black pawn
				Raise(PawnReachedPromotionSquare, new(square, PlayerColor.Black));
		}
	}

	private void CheckGameEndConditions()
	{
		if (_coordinator != null)
		{
			var uciState = _coordinator.State;

			if (uciState.IsCheckmate)
			{
				var winner = State.SideToMove.Opponent();
				EndGame(GameResult.Checkmate(winner));
				return;
			}

			if (uciState.IsStalemate)
			{
				EndGame(GameResult.Stalemate);
				return;
			}
		}

		if (Clock.IsTimeout)
		{
			var winner = Clock.TimedOutPlayer!.Value.Opponent();
			EndGame(GameResult.Timeout(winner));
		}
	}

	private void EndGame(GameResult result)
	{
		StopClock();

		State = State with { Result = result };
		Raise(GameOver, result);
	}

	private void OnClockTick()
	{
		Timer?    timer;
		long      elapsed;
		GameClock updated;

		lock (_sync)
		{
			timer = _clockTimer;
			if (timer == null)
				return;

			elapsed = _clockStopwatch.ElapsedMilliseconds;
			_clockStopwatch.Restart();

			updated = _clock.Tick(elapsed);

			// If the clock was stopped/disposed while we were ticking, don't apply the update.
			if (!ReferenceEquals(_clockTimer, timer))
				return;

			_clock = updated;
		}

		Raise(ClockTick, updated);

		if (updated.IsTimeout) CheckGameEndConditions();
	}

	private void OnError(Exception ex)
	{
		Raise(Error, ex);
	}

	private void OnOpponentDisconnected()
	{
		Raise(OpponentDisconnected);
	}

	private void OnOpponentDrawOffered()
	{
		Raise(DrawOfferReceived);
	}

	private void OnOpponentMoveSubmitted(string move)
	{
		// Move already handled through GetMoveAsync
	}

	private void OnOpponentResigned()
	{
		var result = GameResult.Resignation(PlayerColor);
		EndGame(result);
	}

	private void OnUciStateChanged(UciState uciState)
	{
		var result = DetermineResult(uciState);
		State = GameState.FromUciState(uciState, _history.MovesImmutable, result);

		// Check for pawns on promotion squares after state update
		CheckForPromotionSquares();
	}

	private void Raise<T>(Action<T>? handler, T arg)
	{
		if (handler == null) return;

		if (_syncContext != null)
			_syncContext.Post(_ => handler(arg), null);
		else
			handler(arg);
	}

	private void Raise(Action? handler)
	{
		if (handler == null) return;

		if (_syncContext != null)
			_syncContext.Post(_ => handler(), null);
		else
			handler();
	}

	private void StartClock()
	{
		Timer?    existing;
		GameClock current;
		lock (_sync)
		{
			existing = _clockTimer;
			current  = _clock;

			if (existing != null || current.IsUnlimited)
				return;

			_clockStopwatch.Restart();
			_clock = current.Start();

			_clockTimer = new(
				_ => OnClockTick(),
				null,
				100,
				100
			);
		}
	}

	private void StopClock()
	{
		Timer?    timerToDispose;
		GameClock updated;

		lock (_sync)
		{
			timerToDispose = _clockTimer;
			_clockTimer    = null;

			_clockStopwatch.Stop();

			if (_clock.IsUnlimited)
				return;

			updated = _clock.Stop();
			_clock  = updated;
		}

		try
		{
			timerToDispose?.Change(Timeout.Infinite, Timeout.Infinite);
		}
		catch
		{
			// ignore (timer may already be disposed)
		}

		timerToDispose?.Dispose();

		Raise(ClockTick, updated);
	}

	private void UpdateStateFromCoordinator()
	{
		if (_coordinator == null)
			// Coordinator should always exist (required for all game types)
			// This is a defensive check
			return;

		var uciState = _coordinator.State;
		var result   = DetermineResult(uciState);

		State = GameState.FromUciState(uciState, _history.MovesImmutable, result);

		// Check for pawns on promotion squares after state update
		CheckForPromotionSquares();
	}
}

/// <summary>
///     Information about a promotion piece choice that was set.
/// </summary>
/// <param name="Square">The square where the pawn is located (e.g., "e7").</param>
/// <param name="PromotionPiece">The piece chosen for promotion: 'q' (queen), 'r' (rook), 'b' (bishop), or 'n' (knight).</param>
public readonly record struct PromotionChoiceInfo(string Square, char PromotionPiece);

/// <summary>
///     Information about a pawn that has reached a promotion square.
/// </summary>
/// <param name="Square">The square where the pawn is located (e.g., "e7" for white, "e2" for black).</param>
/// <param name="Color">The color of the pawn that can promote.</param>
public readonly record struct PromotionInfo(string Square, PlayerColor Color);
