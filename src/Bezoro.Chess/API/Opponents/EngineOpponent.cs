using System;
using System.Threading;
using System.Threading.Tasks;
using Bezoro.Chess.API.Abstractions;
using Bezoro.Chess.API.Types;
using Bezoro.UCI.API;
using Bezoro.UCI.API.Types;

namespace Bezoro.Chess.API.Opponents;

/// <summary>
///     Chess engine opponent using UCI protocol.
///     Supports UCI_LimitStrength and UCI_Elo for difficulty control,
///     with fallback to depth/time limits.
/// </summary>
public sealed class EngineOpponent : IOpponent
{
	private readonly string           _enginePath;
	private readonly EngineDifficulty _difficulty;
	private readonly string           _engineName;

	private UciCoordinator? _coordinator;
	private bool            _supportsUciElo;
	private bool            _isDisposed;

	/// <summary>
	///     Creates a new engine opponent.
	/// </summary>
	/// <param name="enginePath">Path to the UCI engine executable.</param>
	/// <param name="difficulty">The difficulty level.</param>
	/// <param name="engineName">Optional display name for the engine.</param>
	public EngineOpponent(
		string            enginePath,
		EngineDifficulty? difficulty = null,
		string?           engineName = null)
	{
		_enginePath = enginePath;
		_difficulty = difficulty ?? EngineDifficulty.Medium;
		_engineName = engineName ?? "Chess Engine";

		Profile = PlayerProfile.CreateEngine(_engineName, _difficulty.Elo);
	}

	/// <inheritdoc />
	public OpponentType Type => OpponentType.Engine;

	/// <inheritdoc />
	public PlayerProfile Profile { get; private set; }

	/// <inheritdoc />
	public bool IsReady => _coordinator?.IsStarted == true;

	/// <summary>
	///     Gets the current difficulty level.
	/// </summary>
	public EngineDifficulty Difficulty => _difficulty;

	/// <summary>
	///     Gets whether the engine supports UCI_Elo option.
	/// </summary>
	public bool SupportsUciElo => _supportsUciElo;

	/// <inheritdoc />
	public event Action<string>? MoveSubmitted;

	/// <inheritdoc />
	public event Action? Resigned;

	/// <inheritdoc />
	public event Action? DrawOffered;

	/// <inheritdoc />
	public event Action? Disconnected;

	/// <inheritdoc />
	public event Action<Exception>? Error;

	/// <inheritdoc />
	public async Task InitializeAsync(CancellationToken ct = default)
	{
		if (_coordinator != null)
			return;

		var options = new UciCoordinatorOptions(
			ClassificationDepth: _difficulty.MaxDepth ?? 6
		);

		_coordinator = await UciCoordinator.CreateAsync(
			_enginePath,
			options,
			null,
			ct
		).ConfigureAwait(false);

		_coordinator.Error += OnCoordinatorError;

		// Try to configure UCI_LimitStrength and UCI_Elo
		await ConfigureDifficultyAsync(ct).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<string?> GetMoveAsync(GameState state, CancellationToken ct = default)
	{
		if (_coordinator == null)
			throw new InvalidOperationException("Engine not initialized. Call InitializeAsync first.");

		try
		{
			// Build search parameters based on difficulty
			var searchParams = BuildSearchParameters();

			// Perform the search
			var result = await _coordinator.SearchAsync(searchParams, ct).ConfigureAwait(false);

			if (!string.IsNullOrEmpty(result.BestMove))
			{
				MoveSubmitted?.Invoke(result.BestMove);
				return result.BestMove;
			}

			return null;
		}
		catch (OperationCanceledException)
		{
			return null;
		}
		catch (Exception ex)
		{
			Error?.Invoke(ex);
			throw;
		}
	}

	/// <inheritdoc />
	public async Task NotifyMovePlayedAsync(string move, GameState state, CancellationToken ct = default)
	{
		if (_coordinator == null)
			return;

		// Update the coordinator's position to match the game state
		// The coordinator tracks position internally, so we may need to sync
		try
		{
			// For now, we just acknowledge the move
			// The ChessGame will handle position updates
			await Task.CompletedTask;
		}
		catch (Exception ex)
		{
			Error?.Invoke(ex);
		}
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		if (_isDisposed)
			return;

		_isDisposed = true;

		if (_coordinator != null)
		{
			_coordinator.Error -= OnCoordinatorError;
			await _coordinator.DisposeAsync().ConfigureAwait(false);
			_coordinator = null;
		}

		Disconnected?.Invoke();
	}

	private async Task ConfigureDifficultyAsync(CancellationToken ct)
	{
		if (_coordinator == null || _difficulty.IsFullStrength)
			return;

		try
		{
			// Try to enable UCI_LimitStrength
			await _coordinator.SetOptionAsync("UCI_LimitStrength", "true", ct).ConfigureAwait(false);

			// Try to set UCI_Elo
			await _coordinator.SetOptionAsync("UCI_Elo", _difficulty.Elo.ToString(), ct).ConfigureAwait(false);

			_supportsUciElo = true;
		}
		catch
		{
			// Engine doesn't support these options, will use depth/time fallback
			_supportsUciElo = false;
		}

		// Also try Skill Level option (used by Stockfish and others)
		try
		{
			// Map ELO to skill level (0-20 scale)
			var skillLevel = MapEloToSkillLevel(_difficulty.Elo);
			await _coordinator.SetOptionAsync("Skill Level", skillLevel.ToString(), ct).ConfigureAwait(false);
		}
		catch
		{
			// Engine doesn't support Skill Level
		}
	}

	private SearchParameters BuildSearchParameters()
	{
		// If UCI_Elo is supported and configured, use longer search times
		// since the engine will play weaker anyway
		if (_supportsUciElo && !_difficulty.IsFullStrength)
		{
			return new SearchParameters
			{
				MoveTimeMs = 2000 // Let the limited engine "think"
			};
		}

		// Fallback: use depth/time limits
		if (_difficulty.MaxDepth.HasValue)
		{
			return new SearchParameters
			{
				Depth = _difficulty.MaxDepth
			};
		}

		if (_difficulty.MaxThinkTimeMs.HasValue)
		{
			return new SearchParameters
			{
				MoveTimeMs = _difficulty.MaxThinkTimeMs
			};
		}

		// Maximum strength - no limits
		return new SearchParameters
		{
			MoveTimeMs = 5000 // Default reasonable think time
		};
	}

	private static int MapEloToSkillLevel(int elo)
	{
		// Map ELO (800-3000+) to Skill Level (0-20)
		// 800 ELO -> Level 0
		// 3000 ELO -> Level 20
		var level = (elo - 800) * 20 / 2200;
		return Math.Clamp(level, 0, 20);
	}

	private void OnCoordinatorError(Exception ex)
	{
		Error?.Invoke(ex);
	}
}

